namespace HRS.Application.Common.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
