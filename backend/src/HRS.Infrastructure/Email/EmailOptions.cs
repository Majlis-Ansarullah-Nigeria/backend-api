namespace HRS.Infrastructure.Email;

public sealed class EmailOptions
{
    public required string From { get; set; }

    public required string SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 1025;
}
