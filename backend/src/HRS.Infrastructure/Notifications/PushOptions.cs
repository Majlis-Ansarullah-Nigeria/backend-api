namespace HRS.Infrastructure.Notifications;

public sealed class PushOptions
{
    public required string VapidSubject { get; set; }

    public required string VapidPublicKey { get; set; }

    public required string VapidPrivateKey { get; set; }
}
