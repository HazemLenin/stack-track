using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class ConcurrencyTests(PostgresFixture db)
{
    [Fact]
    public async Task Concurrent_card_update_throws()
    {
        var graph = await new ConstraintTests(db).SeedGraph();
        await using var first = db.CreateContext();
        await using var second = db.CreateContext();
        var a = await first.Cards.SingleAsync(c => c.Id == graph.Card.Id);
        var b = await second.Cards.SingleAsync(c => c.Id == graph.Card.Id);

        a.Title = "first";
        await first.SaveChangesAsync();
        b.Title = "second";
        var secondSave = () => second.SaveChangesAsync();

        await secondSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Concurrent_list_update_throws()
    {
        var graph = await new ConstraintTests(db).SeedGraph();
        await using var first = db.CreateContext();
        await using var second = db.CreateContext();
        var a = await first.BoardLists.SingleAsync(l => l.Id == graph.List.Id);
        var b = await second.BoardLists.SingleAsync(l => l.Id == graph.List.Id);

        a.Name = "first";
        await first.SaveChangesAsync();
        b.Name = "second";
        var secondSave = () => second.SaveChangesAsync();

        await secondSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
