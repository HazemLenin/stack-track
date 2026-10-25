using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Persistence;

public partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<BoardList> BoardLists => Set<BoardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<CardLabel> CardLabels => Set<CardLabel>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        // Npgsql refuses non-zero offsets for timestamptz, so normalise to UTC on write.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
            entity.FindProperty(nameof(Entity.Id))!.ValueGenerated = ValueGenerated.Never;
            foreach (var property in entity.GetProperties())
                if (property.GetColumnName(table) is { } column && column != "xmin")
                    property.SetColumnName(SnakeCase(column));
            entity.SetTableName(SnakeCase(entity.GetTableName()!));
        }

        // Names derive from table and column names, so they are renamed last.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var key in entity.GetKeys()) key.SetName(SnakeCase(key.GetName()!));
            foreach (var fk in entity.GetForeignKeys()) fk.SetConstraintName(SnakeCase(fk.GetConstraintName()!));
            foreach (var index in entity.GetIndexes()) index.SetDatabaseName(SnakeCase(index.GetDatabaseName()!));
        }
    }

    static string SnakeCase(string name) => WordBoundary().Replace(name, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
