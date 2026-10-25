using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Api.Tests.TestSupport;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Auth;

namespace StackTrack.Api.Tests.Admin;

[Collection(PostgresCollection.Name)]
public class UsersEndpointTests(PostgresFixture db)
{
    const string Password = "Valid1234";

    ApiFactory Factory() => new(connectionString: db.ConnectionString);

    static HttpClient ClientFor(ApiFactory factory, string role, Guid? userId = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiFactory.TokenFor(role, userId));
        return client;
    }

    static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    async Task<User> Seed(string name, UserRole role = UserRole.Member, bool active = true, DateTimeOffset? createdAt = null, string? email = null)
    {
        var user = new User
        {
            DisplayName = name,
            Email = email ?? $"u-{Guid.NewGuid():N}@example.com",
            PasswordHash = new PasswordHasher().Hash(Password),
            Role = role,
            IsActive = active,
        };
        await using var context = db.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        if (createdAt is { } at)
            await context.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, at));
        return user;
    }

    // The database is shared by the whole collection, so tests about the last admin start from one admin.
    async Task<User> SoleAdmin()
    {
        var admin = await Seed("Sole Admin", UserRole.Admin);
        await using var context = db.CreateContext();
        await context.Users.Where(u => u.Role == UserRole.Admin && u.Id != admin.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Member));
        return admin;
    }

    async Task<User> Reload(Guid id)
    {
        await using var context = db.CreateContext();
        return await context.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Fact]
    public async Task List_returns_first_20_sorted_by_name()
    {
        var tag = $"Lst{Guid.NewGuid():N}"[..12];
        for (var i = 0; i < 25; i++) await Seed($"{tag} {i:00}");
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin");

        var response = await client.GetAsync($"/admin/users?search={tag}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Json(response);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("totalCount").GetInt32().Should().Be(25);
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(20);
        items.Select(i => i.GetProperty("displayName").GetString()).Should().BeInAscendingOrder(StringComparer.Ordinal);
        items[0].GetProperty("displayName").GetString().Should().Be($"{tag} 00");
        items[0].GetProperty("role").GetString().Should().Be("Member");
        items[0].GetProperty("isActive").GetBoolean().Should().BeTrue();
        items[0].TryGetProperty("passwordHash", out _).Should().BeFalse();

        var second = await Json(await client.GetAsync($"/admin/users?search={tag}&page=2"));
        second.GetProperty("items").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task List_searches_name_and_email()
    {
        var tag = $"Srch{Guid.NewGuid():N}"[..12];
        await Seed($"Omar {tag}");
        await Seed("Someone Else", email: $"{tag.ToLowerInvariant()}@example.com");
        await Seed("Unrelated");
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin");

        var body = await Json(await client.GetAsync($"/admin/users?search={tag.ToUpperInvariant()}"));

        body.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task List_search_treats_wildcards_literally()
    {
        await Seed($"Plain {Guid.NewGuid():N}");
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin");

        var body = await Json(await client.GetAsync("/admin/users?search=%25"));

        body.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task List_sorts_by_created()
    {
        var tag = $"Srt{Guid.NewGuid():N}"[..10];
        var old = await Seed($"{tag} Zed", createdAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var recent = await Seed($"{tag} Abe", createdAt: new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin");

        var asc = await Json(await client.GetAsync($"/admin/users?search={tag}&sort=created&dir=asc"));
        var desc = await Json(await client.GetAsync($"/admin/users?search={tag}&sort=created&dir=desc"));

        asc.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(old.Id);
        desc.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(recent.Id);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("sort=password")]
    [InlineData("dir=sideways")]
    public async Task List_rejects_invalid_query(string query)
    {
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin");

        (await client.GetAsync($"/admin/users?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Member_gets_403_on_every_user_admin_route()
    {
        using var factory = Factory();
        using var client = ClientFor(factory, "Member");
        (await client.GetAsync("/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
