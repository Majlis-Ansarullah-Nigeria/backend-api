using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Admin.Settings;
using HRS.Application.Features.Analytics;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Review.Compiled;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-ADM-001 (organization settings and branding) and FR-ADM-005 (system health).</summary>
[Collection(ApiTestCollection.Name)]
public class SettingsEndpointsTests(ApiFactory factory)
{
    // A 1×1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private async Task<(HttpClient Client, Guid UserId)> CreateClientAsync(RoleName role)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        const string password = "Correct-Horse-Settings1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, role);
        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private static UpdateOrganizationSettingsRequest Defaults() =>
        new(OrganizationBranding.DefaultName, BrandColor.Green, "Africa/Lagos", DateFormatStyle.DayMonthYear, 1);

    [Fact]
    public async Task EveryoneReadsTheSettingsButOnlyTheSystemAdministratorChangesThem()
    {
        var (admin, adminId) = await CreateClientAsync(RoleName.SysAdmin);
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat);
        var (head, _) = await CreateClientAsync(RoleName.UnitHead);

        try
        {
            var asHead = await head.GetFromJsonAsync<OrganizationSettingsDto>("/api/v1/settings");
            Assert.False(asHead!.CanEdit);

            var refused = await secretariat.PutAsJsonAsync("/api/v1/settings", Defaults() with { Name = "Not allowed" });
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

            var invalid = await admin.PutAsJsonAsync("/api/v1/settings", Defaults() with { TimeZone = "Mars/Olympus" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            var saved = await admin.PutAsJsonAsync("/api/v1/settings",
                new UpdateOrganizationSettingsRequest("Majlis Test", BrandColor.Blue, "Europe/London", DateFormatStyle.Iso, 4));
            var dto = await saved.Content.ReadFromJsonAsync<OrganizationSettingsDto>();
            Assert.Equal(("Majlis Test", BrandColor.Blue, "#1D4ED8", "Europe/London", DateFormatStyle.Iso, 4, true),
                (dto!.Name, dto.BrandColor, dto.BrandPrimary, dto.TimeZone, dto.DateFormat, dto.FiscalYearStartMonth, dto.CanEdit));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = await db.AuditEvents.AsNoTracking().Where(e => e.ActorUserId == adminId && e.Action == AuditActions.SettingsUpdated).ToListAsync();
            Assert.Contains(audit, e => e.AfterJson!.Contains("Majlis Test", StringComparison.Ordinal));
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/v1/settings", Defaults());
        }
    }

    [Fact]
    public async Task ALogoIsCheckedStoredAndServedSandboxed()
    {
        var (admin, _) = await CreateClientAsync(RoleName.SysAdmin);
        try
        {
            using var notAnImage = new MultipartFormDataContent { { new ByteArrayContent("GIF89a"u8.ToArray()), "file", "logo.gif" } };
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsync(new Uri("/api/v1/settings/logo", UriKind.Relative), notAnImage)).StatusCode);

            var png = new ByteArrayContent(TinyPng);
            png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var form = new MultipartFormDataContent { { png, "file", "logo.png" } };
            var uploaded = await admin.PutAsync(new Uri("/api/v1/settings/logo", UriKind.Relative), form);
            Assert.True((await uploaded.Content.ReadFromJsonAsync<OrganizationSettingsDto>())!.HasLogo);

            var logo = await admin.GetAsync(new Uri("/api/v1/settings/logo", UriKind.Relative));
            Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
            Assert.Contains("sandbox", logo.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
            Assert.Equal(TinyPng, await logo.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            await admin.DeleteAsync(new Uri("/api/v1/settings/logo", UriKind.Relative));
        }
    }

    [Fact]
    public async Task TheSecretariatSeesHealthAndRetriesNotificationsTheDispatcherGaveUpOn()
    {
        var (secretariat, secretaryId) = await CreateClientAsync(RoleName.Secretariat);
        var (head, _) = await CreateClientAsync(RoleName.UnitHead);
        Guid messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = OutboxMessage.Create(Guid.CreateVersion7(), NotificationEventType.Reminder, Guid.NewGuid(), "{}", DateTimeOffset.UtcNow);
            for (var i = 0; i < 5; i++)
            {
                message.RecordFailure("SMTP down");
            }

            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        var health = await secretariat.GetFromJsonAsync<SystemHealth>("/api/v1/system/health-summary");
        Assert.True(health!.Notifications.GivenUp >= 1);
        Assert.StartsWith("SMTP", health.EmailTransport, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync(new Uri("/api/v1/system/health-summary", UriKind.Relative))).StatusCode);

        var retried = await (await secretariat.PostAsync(new Uri("/api/v1/system/notifications/retry", UriKind.Relative), null))
            .Content.ReadFromJsonAsync<RetryNotificationsResponse>();
        Assert.True(retried!.Retried >= 1);

        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, (await checkDb.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId)).Attempts);
        Assert.True(await checkDb.AuditEvents.AnyAsync(e => e.ActorUserId == secretaryId && e.Action == AuditActions.NotificationsRetried));
    }

    [Fact]
    public async Task TheCompiledWordExportCarriesTheOrganizationsNameAndLogo()
    {
        using var scope = factory.Services.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<ICompiledReportRenderer>();
        var report = new CompiledReportView(
            Guid.NewGuid(), Guid.NewGuid(), "Monthly Report", "September 2026", null, "NAT-GMR-2026-09", DateTimeOffset.UtcNow, false, "",
            null, new CompiledOverview(0, 0, 0, 0, 0, []), [], [], []);
        var branding = OrganizationBranding.Default with { Name = "Majlis Branding Test", Logo = TinyPng, LogoContentType = "image/png" };

        using var stream = new MemoryStream(renderer.RenderWord(report, branding));
        using var document = WordprocessingDocument.Open(stream, false);

        Assert.Contains("Majlis Branding Test", document.MainDocumentPart!.Document.Body!.InnerText, StringComparison.Ordinal);
        Assert.Single(document.MainDocumentPart.ImageParts);
        Assert.NotEmpty(renderer.RenderPdf(report, branding));
    }
}
