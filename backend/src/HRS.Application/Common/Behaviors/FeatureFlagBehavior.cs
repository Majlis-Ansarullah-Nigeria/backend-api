using HRS.Application.Common.Interfaces;
using Mediator;

namespace HRS.Application.Common.Behaviors;

/// <summary>FR-ADM-004: a request for a switched-off module never reaches its handler.</summary>
public sealed class FeatureFlagBehavior<TMessage, TResponse>(IFeatureFlags featureFlags) : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is IRequiresFeature gated && !await featureFlags.IsEnabledAsync(gated.Feature, cancellationToken))
        {
            throw new FeatureDisabledException(gated.Feature);
        }

        return await next(message, cancellationToken);
    }
}
