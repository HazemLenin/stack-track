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
        var id = Guid.NewGuid();

        (await client.GetAsync("/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/admin/users/{id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync($"/admin/users/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/admin/users/{id}/role", new { role = "Admin" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_blocks_sign_in_and_refresh()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        var omar = await Seed("Omar");
        using var factory = Factory();
        using var anon = factory.CreateApiClient();
        var signIn = await anon.PostAsJsonAsync("/auth/sign-in", new { email = omar.Email, password = Password });
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = signIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refresh_token=")).Split(';')[0];
        using var client = ClientFor(factory, "Admin", admin.Id);

        (await client.PostAsync($"/admin/users/{omar.Id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Reload(omar.Id)).IsActive.Should().BeFalse();
        (await anon.PostAsJsonAsync("/auth/sign-in", new { email = omar.Email, password = Password }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var refresh = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refresh.Headers.Add("Cookie", cookie);
        (await anon.SendAsync(refresh)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using var context = db.CreateContext();
        (await context.RefreshTokens.Where(t => t.UserId == omar.Id && !t.IsRevoked).AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_keeps_owned_boards_and_comments()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        var omar = await Seed("Omar");
        await using (var context = db.CreateContext())
        {
            var board = new Board { OwnerId = omar.Id, Name = "Roadmap" };
            var list = new BoardList { BoardId = board.Id, Name = "To do" };
            var card = new Card { ListId = list.Id, Title = "Plan" };
            context.AddRange(board, list, card, new Comment { CardId = card.Id, AuthorId = omar.Id, Body = "On it" });
            await context.SaveChangesAsync();
        }
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin", admin.Id);

        (await client.PostAsync($"/admin/users/{omar.Id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var check = db.CreateContext();
        (await check.Boards.CountAsync(b => b.OwnerId == omar.Id)).Should().Be(1);
        (await check.Comments.CountAsync(c => c.AuthorId == omar.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Reactivate_allows_sign_in_with_old_password()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        var omar = await Seed("Omar", active: false);
        using var factory = Factory();
        using var anon = factory.CreateApiClient();
        using var client = ClientFor(factory, "Admin", admin.Id);

        (await client.PostAsync($"/admin/users/{omar.Id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Reload(omar.Id)).IsActive.Should().BeTrue();
        (await anon.PostAsJsonAsync("/auth/sign-in", new { email = omar.Email, password = Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Change_role_updates_the_user_and_is_idempotent()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        var omar = await Seed("Omar");
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin", admin.Id);

        (await client.PutAsJsonAsync($"/admin/users/{omar.Id}/role", new { role = "Admin" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/admin/users/{omar.Id}/role", new { role = "Admin" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Reload(omar.Id)).Role.Should().Be(UserRole.Admin);
        (await client.PutAsJsonAsync($"/admin/users/{omar.Id}/role", new { role = "Owner" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_user_returns_404()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin", admin.Id);
        var id = Guid.NewGuid();

        (await client.PostAsync($"/admin/users/{id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync($"/admin/users/{id}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/admin/users/{id}/role", new { role = "Member" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Self_deactivate_and_role_change_return_400()
    {
        var admin = await Seed("Admin", UserRole.Admin);
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin", admin.Id);

        var deactivate = await client.PostAsync($"/admin/users/{admin.Id}/deactivate", null);
        var role = await client.PutAsJsonAsync($"/admin/users/{admin.Id}/role", new { role = "Member" });

        deactivate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        role.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(deactivate)).GetProperty("errors").TryGetProperty("Id", out _).Should().BeTrue();
        var after = await Reload(admin.Id);
        after.IsActive.Should().BeTrue();
        after.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Last_active_admin_cannot_be_deactivated_or_demoted()
    {
        var sole = await SoleAdmin();
        var caller = await Seed("Deactivated Admin", UserRole.Admin, active: false);
        using var factory = Factory();
        using var client = ClientFor(factory, "Admin", caller.Id);

        var deactivate = await client.PostAsync($"/admin/users/{sole.Id}/deactivate", null);
        var demote = await client.PutAsJsonAsync($"/admin/users/{sole.Id}/role", new { role = "Member" });

        deactivate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        demote.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(demote)).GetProperty("detail").GetString().Should().Be("There must be at least one active administrator");
        var after = await Reload(sole.Id);
        after.IsActive.Should().BeTrue();
        after.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Concurrent_demotions_leave_one_admin()
    {
        var a = await SoleAdmin();
        var b = await Seed("Second Admin", UserRole.Admin);
        using var factory = Factory();
        using var asA = ClientFor(factory, "Admin", a.Id);
        using var asB = ClientFor(factory, "Admin", b.Id);

        var results = await Task.WhenAll(
            asA.PutAsJsonAsync($"/admin/users/{b.Id}/role", new { role = "Member" }),
            asB.PutAsJsonAsync($"/admin/users/{a.Id}/role", new { role = "Member" }));

        results.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        await using var context = db.CreateContext();
        (await context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive)).Should().Be(1);
    }
}
