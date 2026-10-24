using HRS.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Email;

public sealed class EmailTransportInfo(IOptions<EmailOptions> options) : IEmailTransportInfo
{
    public string Description => $"SMTP · {options.Value.SmtpHost}:{options.Value.SmtpPort} · from {options.Value.From}";
}
