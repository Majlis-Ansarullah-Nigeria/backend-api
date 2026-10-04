using HRS.Domain.Common;

namespace HRS.Domain.UnitTests.Common;

public class ResultTests
{
    [Fact]
    public void SuccessProducesASucceededResultWithNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void FailureProducesAFailedResultCarryingTheError()
    {
        var error = Error.Conflict("WINDOW_CLOSED", "The submission window is closed.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ConstructingASuccessfulResultWithAnErrorThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void GenericSuccessExposesTheValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void GenericFailureThrowsWhenValueIsAccessed()
    {
        var result = Result.Failure<int>(Error.NotFound("REPORT_NOT_FOUND", "Report instance not found."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversionFromValueProducesASuccessfulResult()
    {
        Result<string> result = "GMR-2026-09-Z03D12M04";

        Assert.True(result.IsSuccess);
        Assert.Equal("GMR-2026-09-Z03D12M04", result.Value);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Forbidden)]
    [InlineData(ErrorType.Unexpected)]
    public void ErrorFactoryHelpersSetTheMatchingType(ErrorType expectedType)
    {
        Error error = expectedType switch
        {
            ErrorType.Validation => Error.Validation("CODE", "message"),
            ErrorType.NotFound => Error.NotFound("CODE", "message"),
            ErrorType.Conflict => Error.Conflict("CODE", "message"),
            ErrorType.Forbidden => Error.Forbidden("CODE", "message"),
            _ => Error.Unexpected("CODE", "message"),
        };

        Assert.Equal(expectedType, error.Type);
        Assert.Equal("CODE", error.Code);
    }
}
