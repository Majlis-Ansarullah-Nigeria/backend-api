namespace HRS.Infrastructure.Security;

public sealed class ClamAvOptions
{
    public required string Host { get; set; }

    public int Port { get; set; } = 3310;
}
