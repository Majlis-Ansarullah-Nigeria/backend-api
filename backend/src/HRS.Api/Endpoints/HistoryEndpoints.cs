using HRS.Api.Common;
using HRS.Application.Features.History;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.History;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var history = app.MapGroup("/api/v1/history").WithTags("History").RequireAuthorization();

        history.MapGet("/", async (
                [FromQuery] Guid? reportTypeId,
                [FromQuery] Guid? fromWindowId,
                [FromQuery] Guid? toWindowId,
                [FromQuery] ReportLineStatus? status,
                [FromQuery] string? search,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetHistoryQuery(
                    user.GetUserId(), reportTypeId, fromWindowId, toWindowId, status, search, page ?? 1, pageSize ?? 20);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetHistory")
            .Produces<HistoryPage>();

        history.MapGet("/filters", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetHistoryFiltersQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetHistoryFilters")
            .Produces<HistoryFilterOptions>();

        history.MapGet("/{instanceId:guid}/pdf", async (
                Guid instanceId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetHistoryReportPdfQuery(user.GetUserId(), instanceId), cancellationToken);

                return result.IsSuccess
                    ? Results.File(result.Value, "application/pdf", "report.pdf")
                    : result.ToProblemDetails();
            })
            .WithName("GetHistoryReportPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        history.MapGet("/{instanceId:guid}/excel", async (
                Guid instanceId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetHistoryReportExcelQuery(user.GetUserId(), instanceId), cancellationToken);

                return result.IsSuccess
                    ? Results.File(result.Value, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "report.xlsx")
                    : result.ToProblemDetails();
            })
            .WithName("GetHistoryReportExcel")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        history.MapGet("/compare", async (
                [FromQuery] Guid unitId,
                [FromQuery] Guid reportTypeId,
                [FromQuery] Guid? periodA,
                [FromQuery] Guid? periodB,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetPeriodComparisonQuery(user.GetUserId(), unitId, reportTypeId, periodA, periodB);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetPeriodComparison")
            .Produces<PeriodComparison>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        history.MapGet("/compare/options", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetComparisonOptionsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetComparisonOptions")
            .Produces<IReadOnlyList<ComparisonUnitOption>>();

        // SRS §9's `GET /reports/{id}/revisions` — mapped here because it shares History's access
        // rule (plus the Secretariat), not Submissions' head-only one.
        app.MapGet("/api/v1/reports/{reportId:guid}/revisions", async (
                Guid reportId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetReportRevisionsQuery(user.GetUserId(), reportId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithTags("History")
            .RequireAuthorization()
            .WithName("GetReportRevisions")
            .Produces<ReportRevisionHistory>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        history.MapPost("/bulk-export", async (
                [FromBody] CreateBulkExportRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateBulkExportCommand(user.GetUserId(), request.InstanceIds, request.Format);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CreateBulkExport")
            .Produces<ExportSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}

public sealed record CreateBulkExportRequest(IReadOnlyList<Guid> InstanceIds, BulkExportFormat Format);
