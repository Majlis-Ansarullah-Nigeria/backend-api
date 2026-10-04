using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Common;

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
