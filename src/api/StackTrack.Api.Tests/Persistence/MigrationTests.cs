using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class MigrationTests(PostgresFixture db)
{
    [Fact]
    public async Task Migration_applies_to_empty_database()
    {
        await using var context = FreshDatabase();

        await context.Database.MigrateAsync();

        var tables = await context.Database.SqlQuery<string>(
            $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync();
        tables.Should().Contain([
            "user", "board", "board_member", "list", "card",
            "card_assignee", "label", "card_label", "comment", "refresh_token"]);
    }

    [Fact]
    public async Task Migration_applied_twice_is_noop()
    {
        await using var context = FreshDatabase();
        await context.Database.MigrateAsync();
        var before = await AppliedCount(context);

        await context.Database.MigrateAsync();

        (await AppliedCount(context)).Should().Be(before).And.BePositive();
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("board_member", "(user_id)")]
    [InlineData("card", "(list_id, \"position\")")]
    [InlineData("card_assignee", "(user_id)")]
    [InlineData("list", "(board_id, \"position\")")]
    public async Task Query_indexes_exist(string table, string columns)
    {
        await using var context = db.CreateContext();

        var indexes = await context.Database.SqlQuery<string>(
            $"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = {table}").ToListAsync();

        indexes.Should().Contain(d => d.Contains(columns) && !d.Contains("UNIQUE"));
    }

    Infrastructure.Persistence.AppDbContext FreshDatabase() =>
        db.CreateContext(connectionString: new NpgsqlConnectionStringBuilder(db.ConnectionString)
        {
            Database = $"fresh_{Guid.NewGuid():N}",
        }.ConnectionString);

    static async Task<int> AppliedCount(DbContext context) =>
        (await context.Database.GetAppliedMigrationsAsync()).Count();
}
