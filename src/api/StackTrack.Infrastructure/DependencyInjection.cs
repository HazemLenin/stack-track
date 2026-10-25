using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackTrack.Application.Auth;
using StackTrack.Infrastructure.Auth;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => o.Secret.Length >= 32, "Jwt:Secret must be at least 32 characters.")
            .ValidateOnStart();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}
