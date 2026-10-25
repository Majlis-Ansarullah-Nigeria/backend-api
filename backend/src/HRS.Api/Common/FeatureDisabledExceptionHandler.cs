using HRS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Common;

/// <summary>FR-ADM-004: a request for a switched-off module gets 403 with the stable code
/// FEATURE_DISABLED, so the UI can tell it apart from a permission problem.</summary>
public sealed class FeatureDisabledExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not FeatureDisabledException disabled)
        {
            return false;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "This module is switched off.",
            Detail = disabled.Message,
        };
        problem.Extensions["code"] = "FEATURE_DISABLED";

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
