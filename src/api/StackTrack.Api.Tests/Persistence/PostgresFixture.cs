using Microsoft.EntityFrameworkCore;
using StackTrack.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StackTrack.Api.Tests.Persistence;

public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public AppDbContext CreateContext(TimeProvider? clock = null, string? connectionString = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString ?? ConnectionString)
            .AddInterceptors(new AuditInterceptor(clock ?? TimeProvider.System))
            .Options);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}
