using HRS.Application.Common.Models;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed record VerifyOtpCommand(
    string Identifier,
    string Code,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<LoginOutcome>>;
