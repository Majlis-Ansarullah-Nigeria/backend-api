# Backend templates

## Command, handler, validator
```csharp
namespace HRS.Application.Features.Submissions.SubmitReport;

public sealed record SubmitReportCommand(Guid ReportId, string IdempotencyKey, bool Declaration)
    : IRequest<Result<SubmitReportResponse>>;

public sealed record SubmitReportResponse(Guid ReportId, string ReferenceNo, DateTimeOffset SubmittedAt, bool IsLate);

internal sealed class SubmitReportValidator : AbstractValidator<SubmitReportCommand>
{
    public SubmitReportValidator()
    {
        RuleFor(x => x.ReportId).NotEmpty();
        RuleFor(x => x.Declaration).Equal(true).WithErrorCode("DECLARATION_REQUIRED");
    }
}

internal sealed class SubmitReportHandler(
    IAppDbContext db, IAccessPolicy access, ICurrentUser user, IClock clock)
    : IRequestHandler<SubmitReportCommand, Result<SubmitReportResponse>>
{
    public async Task<Result<SubmitReportResponse>> Handle(SubmitReportCommand cmd, CancellationToken ct)
    {
        var report = await db.ReportInstances
            .Include(r => r.Window)
            .SingleOrDefaultAsync(r => r.Id == cmd.ReportId, ct);
        if (report is null) return ReportErrors.NotFound;

        var canSubmit = await access.CanSubmitAsync(user, report, ct);
        if (!canSubmit) return AccessErrors.Forbidden;

        var result = report.Submit(clock, user.ActiveAssignment);
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return new SubmitReportResponse(report.Id, report.ReferenceNo!, report.SubmittedAt!.Value, report.IsLate);
    }
}
```

## Domain method
```csharp
public Result Submit(IClock clock, AssignmentRef actor)
{
    if (Status is not (ReportStatus.Draft or ReportStatus.Returned))
        return ReportErrors.InvalidState(Status);

    var now = clock.UtcNow;
    var deadlines = Window.DeadlinesFor(UnitId, UnitLevel);   // includes extensions
    if (now > deadlines.ClosesAt && Status != ReportStatus.Returned)
        return ReportErrors.WindowClosed;

    Status = ReportStatus.Submitted;
    IsLate = now > deadlines.DueAt;
    SubmittedAt = now;
    SubmittedBy = actor;
    ReferenceNo ??= ReferenceNumber.For(Window.ReportType.Code, Window.PeriodStart, UnitCode);
    Raise(new ReportSubmitted(Id, UnitId, IsLate, now));
    return Result.Success();
}
```

## Endpoint
```csharp
public static class SubmissionsEndpoints
{
    public static IEndpointRouteBuilder MapSubmissions(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/reports").WithTags("Reports").RequireAuthorization();

        g.MapPost("/{id:guid}/submit", async (Guid id, SubmitReportRequest body,
                [FromHeader(Name = "Idempotency-Key")] string key, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SubmitReportCommand(id, key, body.Declaration), ct))
                .Match(Results.Ok, err => err.ToProblemDetails()))
            .WithName("SubmitReport")
            .Produces<SubmitReportResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
```

## Integration test
```csharp
public sealed class SubmitReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Observer_cannot_submit_child_report()
    {
        var seed = await factory.SeedAsync(s => s.MuqamWithDraft().DilaHeadObserver());
        var client = factory.ClientFor(seed.DilaHead);

        var res = await client.PostAsJsonAsync($"/api/v1/reports/{seed.MuqamReportId}/submit",
            new { declaration = true }, headers: new() { ["Idempotency-Key"] = Guid.NewGuid().ToString() });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```
