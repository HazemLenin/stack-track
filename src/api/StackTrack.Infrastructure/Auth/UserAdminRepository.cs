using Microsoft.EntityFrameworkCore;
using StackTrack.Application.Admin.Users;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure.Auth;

public class UserAdminRepository(AppDbContext db, TimeProvider clock) : IUserAdminRepository
{
    public async Task<UserPage> ListAsync(string? search, string sort, string dir, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(u => EF.Functions.ILike(u.DisplayName, pattern, "\\") || EF.Functions.ILike(u.Email, pattern, "\\"));
        }

        var total = await query.CountAsync(ct);
        var ordered = (sort, dir) switch
        {
            ("created", "desc") => query.OrderByDescending(u => u.CreatedAt),
            ("created", _) => query.OrderBy(u => u.CreatedAt),
            (_, "desc") => query.OrderByDescending(u => u.DisplayName),
            _ => query.OrderBy(u => u.DisplayName),
        };
        var rows = await ordered.ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.Role, u.IsActive, u.CreatedAt })
            .ToListAsync(ct);

        return new UserPage(
            rows.Select(u => new UserListItem(u.Id, u.DisplayName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt)).ToList(),
            page, pageSize, total);
    }

    public Task<UserChange> SetActiveAsync(Guid id, bool isActive, CancellationToken ct) =>
        ApplyAsync(id, removesAdmin: !isActive, async users =>
        {
            var now = clock.GetUtcNow();
            var rows = await users.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsActive, isActive)
                .SetProperty(u => u.UpdatedAt, now), ct);
            if (rows > 0 && !isActive)
                await db.RefreshTokens.Where(t => t.UserId == id && !t.IsRevoked)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true).SetProperty(t => t.UpdatedAt, now), ct);
            return rows;
        }, ct);

    public Task<UserChange> SetRoleAsync(Guid id, UserRole role, CancellationToken ct) =>
        ApplyAsync(id, removesAdmin: role != UserRole.Admin, users =>
        {
            var now = clock.GetUtcNow();
            return users.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, role)
                .SetProperty(u => u.UpdatedAt, now), ct);
        }, ct);

    // The advisory lock serialises admin changes: under READ COMMITTED two concurrent guarded UPDATEs on
    // different rows would each see the other admin as still active and both succeed.
    async Task<UserChange> ApplyAsync(Guid id, bool removesAdmin, Func<IQueryable<User>, Task<int>> update, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(21)", ct);

        var rows = await update(db.Users.Where(u => u.Id == id &&
            (!removesAdmin || !(u.IsActive && u.Role == UserRole.Admin) ||
             db.Users.Any(o => o.Id != id && o.IsActive && o.Role == UserRole.Admin))));
        var exists = rows > 0 || await db.Users.AnyAsync(u => u.Id == id, ct);
        await tx.CommitAsync(ct);

        return rows > 0 ? UserChange.Done : exists ? UserChange.LastAdmin : UserChange.NotFound;
    }
}
