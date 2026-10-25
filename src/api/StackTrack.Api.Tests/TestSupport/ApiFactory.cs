using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StackTrack.Api.Tests.TestSupport;

public class ApiFactory(
    string environment = "Production",
    ILoggerProvider? loggerProvider = null,
    string connectionString = "Host=localhost;Port=1;Timeout=1;Username=x;Password=x;Database=x")
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Default", connectionString);

        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestController).Assembly);
            services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<TestController>());
            services.AddValidatorsFromAssemblyContaining<ValidateCommandValidator>();
        });

        if (loggerProvider is not null)
            builder.ConfigureLogging(l => l.AddProvider(loggerProvider));
    }
}
