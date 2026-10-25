using HRS.Api.Common;
using HRS.Application.Features.Search;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/search", async (
                [FromQuery] string? q,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GlobalSearchQuery(user.GetUserId(), q), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithTags("Search")
            .RequireAuthorization()
            .WithName("GlobalSearch")
            .Produces<GlobalSearchResults>();
    }
}
