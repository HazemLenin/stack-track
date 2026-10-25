using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Api.Tests.Persistence;

public partial class ModelTests
{
    static readonly IModel Model = BuildModel();

    static IModel BuildModel()
    {
        using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost").Options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Tables_are_the_ten_erd_entities_in_snake_case() =>
        Model.GetEntityTypes().Select(e => e.GetTableName()).Should().BeEquivalentTo(
            "user", "board", "board_member", "list", "card",
            "card_assignee", "label", "card_label", "comment", "refresh_token");

    [Fact]
    public void Every_column_and_constraint_name_is_snake_case()
    {
        foreach (var entity in Model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
            NotSnakeCase(entity.GetProperties().Select(p => p.GetColumnName(table))).Should().BeEmpty();
            NotSnakeCase(entity.GetKeys().Select(k => k.GetName())).Should().BeEmpty();
            NotSnakeCase(entity.GetForeignKeys().Select(f => f.GetConstraintName())).Should().BeEmpty();
            NotSnakeCase(entity.GetIndexes().Select(i => i.GetDatabaseName())).Should().BeEmpty();
        }
    }

    static string?[] NotSnakeCase(IEnumerable<string?> names) => names.Where(n => !SnakeCase().IsMatch(n!)).ToArray();

    [Fact]
    public void Primary_keys_are_uuid_and_every_timestamp_is_timestamptz()
    {
        foreach (var entity in Model.GetEntityTypes())
        {
            entity.FindPrimaryKey()!.Properties.Single().GetColumnType().Should().Be("uuid");
            foreach (var p in entity.GetProperties()
                         .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTimeOffset)))
                p.GetColumnType().Should().Be("timestamp with time zone", $"{entity.GetTableName()}.{p.Name}");
        }
    }

    [Theory]
    [InlineData("board", "user", DeleteBehavior.Restrict)]
    [InlineData("board_member", "board", DeleteBehavior.Cascade)]
    [InlineData("board_member", "user", DeleteBehavior.Restrict)]
    [InlineData("list", "board", DeleteBehavior.Cascade)]
    [InlineData("card", "list", DeleteBehavior.Cascade)]
    [InlineData("label", "board", DeleteBehavior.Cascade)]
    [InlineData("card_assignee", "card", DeleteBehavior.Cascade)]
    [InlineData("card_assignee", "user", DeleteBehavior.Restrict)]
    [InlineData("card_label", "card", DeleteBehavior.Cascade)]
    [InlineData("card_label", "label", DeleteBehavior.Cascade)]
    [InlineData("comment", "card", DeleteBehavior.Cascade)]
    [InlineData("comment", "user", DeleteBehavior.Restrict)]
    [InlineData("refresh_token", "user", DeleteBehavior.Restrict)]
    [InlineData("refresh_token", "refresh_token", DeleteBehavior.SetNull)]
    public void Delete_behavior_matches_erd(string dependent, string principal, DeleteBehavior expected) =>
        Table(dependent).GetForeignKeys()
            .Single(f => f.PrincipalEntityType.GetTableName() == principal)
            .DeleteBehavior.Should().Be(expected);

    [Theory]
    [InlineData("card")]
    [InlineData("list")]
    public void Card_and_list_use_xmin_as_concurrency_token(string table)
    {
        var version = Table(table).FindProperty("Version")!;
        version.IsConcurrencyToken.Should().BeTrue();
        version.GetColumnName(StoreObjectIdentifier.Table(table)).Should().Be("xmin");
    }

    static IEntityType Table(string name) => Model.GetEntityTypes().Single(e => e.GetTableName() == name);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex SnakeCase();
}
