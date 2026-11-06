using System.Net;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Analytics;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Review.Compiled;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-REV-006 (compiled national report, PDF and Word) and FR-DSH-004 (map view).</summary>
[Collection(ApiTestCollection.Name)]
public class CompiledReportsAndMapTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<(HttpClient Client, Guid UserId)> CreateClientAsync(RoleName? role, Guid unitId)
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Comp1";
        var userId = await factory.CreateUserAsync(email, password);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, unitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private sealed record Setup(HttpClient Secretariat, Guid ReportTypeId, Guid ZoneId, Guid DilaAId, Guid DilaBId, Guid MuqamId, Guid CurrentWindowId);

    /// <summary>A General Monthly report filed by Dilaat and Muqamaat, with one figure (members,
    /// Sum). One zone: Dila A (GPS-pinned, with one Muqam) and Dila B. Windows target exactly
    /// these three units. August: both Dilaat acknowledged (100, 60). September: Dila A
    /// acknowledged (110), Dila B submitted but not acknowledged (999), the Muqam nothing.</summary>
    private async Task<Setup> SetUpAsync(string suffix)
    {
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId);

        var reportType = await (await secretariat.PostAsJsonAsync("/api/v1/report-types", new CreateReportTypeRequest(
                $"Compiled Report {suffix}", null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
                [UnitLevelCode.Dila, UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: false, AllowNilReport: false,
                Code: $"CR{suffix}")))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        var members = new TemplateField("members", TemplateFieldType.Number, "Total members", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.Sum, null, null, null);
        var draft = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportType!.Id}/template-versions",
                new UpdateTemplateSchemaRequest(new TemplateSchema([new TemplateSection("membership", "Membership", [members])]))))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await secretariat.PostAsync(new Uri($"/api/v1/report-types/{reportType.Id}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);

        // "CP"-prefixed codes — the shared Testcontainers Postgres needs per-file namespacing.
        async Task<Guid> CreateUnitAsync(string code, string name, Guid levelId, Guid parentId, double? lat = null, double? lng = null) =>
            (await (await secretariat.PostAsJsonAsync("/api/v1/units",
                new CreateUnitRequest(code, name, levelId, parentId, null, null, null, lat, lng)))
                .Content.ReadFromJsonAsync<UnitSummary>())!.Id;

        var zoneId = await CreateUnitAsync($"CPZ{suffix}", $"Compiled Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId);
        var dilaAId = await CreateUnitAsync($"CPDA{suffix}", $"Compiled Dila A {suffix}", OrganizationSeedIds.DilaLevelId, zoneId, 7.38, 3.94);
        var dilaBId = await CreateUnitAsync($"CPDB{suffix}", $"Compiled Dila B {suffix}", OrganizationSeedIds.DilaLevelId, zoneId);
        var muqamId = await CreateUnitAsync($"CPM{suffix}", $"Compiled Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaAId);
        Guid[] targets = [dilaAId, dilaBId, muqamId];

        var toAcknowledge = new List<Guid>();
        Guid currentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var versionId = (await db.TemplateVersions.FirstAsync(t => t.ReportTypeId == reportType.Id && t.Status == TemplateVersionStatus.Published)).Id;
            WindowLevelDueDate[] Due(DateTimeOffset at) => [new(UnitLevelCode.Dila, at), new(UnitLevelCode.Muqam, at)];
            var august = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "August 2026", Now.AddDays(-40), 3, Now.AddDays(-25),
                Due(Now.AddDays(-32)), clock.UtcNow, targetUnitIds: targets);
            var september = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "September 2026", Now.AddDays(-5), 3, Now.AddDays(15),
                Due(Now.AddDays(5)), clock.UtcNow, targetUnitIds: targets);
            db.SubmissionWindows.AddRange(august, september);
            currentId = september.Id;

            ReportInstance Submitted(SubmissionWindow window, Guid unitId, int members)
            {
                var instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, unitId, versionId, clock.UtcNow);
                instance.Save($$"""{"members":{{members}}}""");
                instance.Submit(isLate: false, $"REF-CR-{Guid.NewGuid():N}", isNil: false, Now);
                db.ReportInstances.Add(instance);
                return instance;
            }

            toAcknowledge.Add(Submitted(august, dilaAId, 100).Id);
            toAcknowledge.Add(Submitted(august, dilaBId, 60).Id);
            toAcknowledge.Add(Submitted(september, dilaAId, 110).Id);
            Submitted(september, dilaBId, 999);
            await db.SaveChangesAsync();
        }

        foreach (var id in toAcknowledge)
        {
            await secretariat.PostAsync(new Uri($"/api/v1/secretariat/inbox/{id}/acknowledge", UriKind.Relative), null);
        }

        return new Setup(secretariat, reportType.Id, zoneId, dilaAId, dilaBId, muqamId, currentId);
    }

    [Fact]
    public async Task TheCompiledReportSummarisesTheWindowFromAcknowledgedReports()
    {
        var setup = await SetUpAsync("01");
        using var secretariat = setup.Secretariat;

        var report = await secretariat.GetFromJsonAsync<CompiledReportView>($"/api/v1/compiled-reports/{setup.CurrentWindowId}");

        Assert.Equal("NAT-CR01-2026-09", report!.ReferenceNumber);
        Assert.Equal("August 2026", report.PreviousPeriodLabel);
        Assert.True(report.CanEdit);
        Assert.Equal((3, 2, 0, 1, 1), (report.Overview.Expected, report.Overview.OnTime, report.Overview.Late, report.Overview.NotReported, report.Overview.Acknowledged));
        Assert.Equal([(UnitLevelCode.Muqam, 0, 1), (UnitLevelCode.Dila, 2, 2)], report.Overview.Levels.Select(l => (l.Level, l.Submitted, l.Expected)));

        var membership = Assert.Single(report.Sections);
        var figure = Assert.Single(membership.Figures);
        Assert.Equal(("members", 110d, 160d), (figure.FieldKey, figure.Value!.Value, figure.Previous!.Value));
        Assert.Contains(membership.Highlights, h => h.Kind == CompiledHighlightKind.Highest && h.ZoneName == "Compiled Zone 01" && h.Value == 110);
        Assert.Contains(membership.Highlights, h => h.Kind == CompiledHighlightKind.LargestFall && h.Change == -50);

        var zone = Assert.Single(report.Zones, z => z.UnitId == setup.ZoneId);
        Assert.Equal((2, 3, 110d), (zone.Submitted, zone.Expected, zone.Values.Single()!.Value));
    }

    [Fact]
    public async Task TheSecretariatEditsTheNarrativeAndLeadershipOnlyReadsIt()
    {
        var setup = await SetUpAsync("02");
        using var secretariat = setup.Secretariat;
        var (leadership, _) = await CreateClientAsync(RoleName.Leadership, OrganizationSeedIds.NationalUnitId);
        var (head, _) = await CreateClientAsync(RoleName.UnitHead, setup.DilaAId);
        var url = $"/api/v1/compiled-reports/{setup.CurrentWindowId}";

        var saved = await secretariat.PutAsJsonAsync($"{url}/narrative", new UpdateCompiledNarrativeRequest("  Membership grew in Dila A.  "));
        var byLeadership = await leadership.PutAsJsonAsync($"{url}/narrative", new UpdateCompiledNarrativeRequest("Edited"));
        var leadershipView = await leadership.GetFromJsonAsync<CompiledReportView>(url);
        var headView = await head.GetAsync(new Uri(url, UriKind.Relative));
        var list = await leadership.GetFromJsonAsync<List<CompiledReportListItem>>("/api/v1/compiled-reports");

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byLeadership.StatusCode);
        Assert.Equal(("Membership grew in Dila A.", false), (leadershipView!.Narrative, leadershipView.CanEdit));
        Assert.Equal(HttpStatusCode.Forbidden, headView.StatusCode);
        Assert.True(list!.Single(i => i.WindowId == setup.CurrentWindowId).HasNarrative);
    }

    [Fact]
    public async Task TheReportDownloadsAsPdfAndAsAWordDocument()
    {
        var setup = await SetUpAsync("03");
        using var secretariat = setup.Secretariat;
        var url = $"/api/v1/compiled-reports/{setup.CurrentWindowId}";
        await secretariat.PutAsJsonAsync($"{url}/narrative", new UpdateCompiledNarrativeRequest("A strong month for membership."));

        var pdf = await secretariat.GetAsync(new Uri($"{url}/pdf", UriKind.Relative));
        var word = await secretariat.GetAsync(new Uri($"{url}/word", UriKind.Relative));

        Assert.True(pdf.IsSuccessStatusCode, await pdf.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await pdf.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", word.Content.Headers.ContentType?.MediaType);
        Assert.Equal("NAT-CR03-2026-09.docx", word.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        using var stream = new MemoryStream(await word.Content.ReadAsByteArrayAsync());
        using var document = WordprocessingDocument.Open(stream, false);
        var text = document.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("A strong month for membership.", text, StringComparison.Ordinal);
        Assert.Contains("Total members", text, StringComparison.Ordinal);
        Assert.Contains("Compiled Zone 03", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMapColoursAZonesDilaatByAFigureOrByComplianceAndPinsGpsUnits()
    {
        var setup = await SetUpAsync("04");
        using var secretariat = setup.Secretariat;
        var baseUrl = $"/api/v1/dashboards/map?reportTypeId={setup.ReportTypeId}&unitId={setup.ZoneId}";

        var byFigure = await secretariat.GetFromJsonAsync<MapView>(baseUrl);
        var byCompliance = await secretariat.GetFromJsonAsync<MapView>($"{baseUrl}&measure={(int)MapMeasure.Compliance}");

        Assert.Equal(("members", "September 2026"), (byFigure!.Field!.FieldKey, byFigure.PeriodLabel));
        Assert.Equal(
            [("CPDA04", (double?)110, true), ("CPDB04", (double?)null, false)],
            byFigure.Tiles.Select(t => (t.Code, t.Value, t.HasChildren)));
        Assert.Equal([setup.DilaAId], byFigure.Pins.Select(p => p.UnitId));
        Assert.Equal((7.38, 3.94), (byFigure.Pins[0].Latitude!.Value, byFigure.Pins[0].Longitude!.Value));

        Assert.Equal([("CPDA04", 50d), ("CPDB04", 100d)], byCompliance!.Tiles.Select(t => (t.Code, t.Value!.Value)));
    }

    [Fact]
    public async Task ADilaHeadsMapStaysInsideTheirSubtree()
    {
        var setup = await SetUpAsync("05");
        setup.Secretariat.Dispose();
        var (dilaHead, _) = await CreateClientAsync(RoleName.UnitHead, setup.DilaAId);

        var own = await dilaHead.GetAsync(new Uri($"/api/v1/dashboards/map?reportTypeId={setup.ReportTypeId}", UriKind.Relative));
        var zone = await dilaHead.GetAsync(new Uri($"/api/v1/dashboards/map?reportTypeId={setup.ReportTypeId}&unitId={setup.ZoneId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, zone.StatusCode);
    }
}
