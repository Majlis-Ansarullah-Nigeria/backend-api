namespace HRS.Domain.Common;

public enum ErrorType
{
    None,
    Validation,
    Unauthenticated,
    NotFound,
    Conflict,
    Forbidden,
    Unexpected,
}
