using HRS.Application.Common.Interfaces;

namespace HRS.Application.Common.Models;

public sealed record AuthResult(Guid UserId, string Name, string Email, AuthTokens Tokens);
