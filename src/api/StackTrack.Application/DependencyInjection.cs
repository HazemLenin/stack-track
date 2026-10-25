using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using StackTrack.Application.Behaviors;
using StackTrack.Application.Boards;

namespace StackTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(c => c.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BoardAccessBehavior<,>));

        return services;
    }
}
