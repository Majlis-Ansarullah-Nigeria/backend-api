using HRS.Api.Common;
using HRS.Application.Features.History;
using HRS.Application.Features.Submissions.MyReports;
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
    }
}
