using System.Globalization;
using HRS.Domain.Common;

namespace HRS.Api.Common;

public static class ResultExtensions
{
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");
        }

        var statusCode = result.Error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = Results.Problem(
            statusCode: statusCode,
            title: result.Error.Code,
            detail: result.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });

        return result.Error.RetryAfterSeconds is { } retryAfterSeconds
            ? new RetryAfterResult(problem, retryAfterSeconds)
            : problem;
    }

    private sealed class RetryAfterResult(IResult inner, int retryAfterSeconds) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return inner.ExecuteAsync(httpContext);
        }
    }
}
