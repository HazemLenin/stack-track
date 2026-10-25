using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace StackTrack.Api.Tests.TestSupport;

public class ApiFactory(
    string environment = "Production",
    ILoggerProvider? loggerProvider = null,
    string connectionString = "Host=localhost;Port=1;Timeout=1;Username=x;Password=x;Database=x",
    TimeProvider? clock = null)
    : WebApplicationFactory<Program>
{
    public const string JwtSecret = "test-secret-test-secret-test-secret-0123456789";

    // https so the Secure refresh cookie is sent back by the test client's cookie container.
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // A valid access token signed with the test key, for tests that need no database.
    public static string TokenFor(string role, Guid? userId = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "stacktrack-tests",
            Audience = "stacktrack-tests",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity([new Claim("sub", (userId ?? Guid.NewGuid()).ToString()), new Claim("role", role)]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)), SecurityAlgorithms.HmacSha256),
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", "stacktrack-tests");
        builder.UseSetting("Jwt:Audience", "stacktrack-tests");
        builder.UseSetting("Jwt:RefreshCookiePath", "/auth");

        builder.ConfigureServices(services =>
        {
            // Last registration wins, so this replaces TimeProvider.System from AddInfrastructure.
            if (clock is not null) services.AddSingleton(clock);
            services.AddControllers().AddApplicationPart(typeof(TestController).Assembly);
            services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<TestController>());
            services.AddValidatorsFromAssemblyContaining<ValidateCommandValidator>();
        });

        if (loggerProvider is not null)
            builder.ConfigureLogging(l => l.AddProvider(loggerProvider));
    }
}
