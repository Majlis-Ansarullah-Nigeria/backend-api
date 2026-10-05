using FluentValidation;
using HRS.Application.Common.Behaviors;
using HRS.Application.Features.Identity.Login;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();

        return services;
    }
}
