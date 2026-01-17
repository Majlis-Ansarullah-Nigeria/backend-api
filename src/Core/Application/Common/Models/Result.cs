namespace ManagementApi.Application.Common.Models;

public class Result
{
    public bool Succeeded { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();

    public static Result Success()
    {
        return new Result { Succeeded = true };
    }

    public static Result Success(string message)
    {
        return new Result { Succeeded = true, Messages = new[] { message } };
    }

    public static Result Failure(params string[] errors)
    {
        return new Result { Succeeded = false, Messages = errors };
    }
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Success(T data)
    {
        return new Result<T> { Succeeded = true, Data = data };
    }

    public static Result<T> Success(T data, string message)
    {
        return new Result<T> { Succeeded = true, Data = data, Messages = new[] { message } };
    }

    public new static Result<T> Failure(params string[] errors)
    {
        return new Result<T> { Succeeded = false, Messages = errors };
    }
}
