using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Notifications.Push;

public sealed record GetVapidPublicKeyQuery : IRequest<Result<string>>;

/// <summary>PushOptions lives in Infrastructure (it's pure config, same as EmailOptions) — this
/// handler only needs the public half, injected as the generic IOptions&lt;T&gt; interface so
/// Application never references Infrastructure's concrete type.</summary>
public sealed class GetVapidPublicKeyQueryHandler(IOptions<PushPublicKeyOptions> options) : IRequestHandler<GetVapidPublicKeyQuery, Result<string>>
{
    public ValueTask<Result<string>> Handle(GetVapidPublicKeyQuery request, CancellationToken cancellationToken) =>
        new(Result.Success(options.Value.VapidPublicKey));
}

/// <summary>The one PushOptions field the Application layer is allowed to see.</summary>
public sealed class PushPublicKeyOptions
{
    public required string VapidPublicKey { get; set; }
}
