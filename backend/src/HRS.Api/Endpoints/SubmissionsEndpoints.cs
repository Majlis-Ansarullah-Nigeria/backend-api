using HRS.Api.Common;
using HRS.Application.Features.Submissions.MyReports;
using Mediator;

namespace HRS.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static void MapSubmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var myReports = app.MapGroup("/api/v1/me/reports").WithTags("Submissions").RequireAuthorization();

        myReports.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyReportsQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetMyReports")
            .Produces<IReadOnlyList<MyReportSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
