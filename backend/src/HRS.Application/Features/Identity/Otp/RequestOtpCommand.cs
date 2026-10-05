using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed record RequestOtpCommand(string Identifier) : IRequest<Result>;
