using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackTrack.Application.Auth;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure.Auth;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<DateTimeOffset?> RecordFailedLoginAsync(
        Guid userId, DateTimeOffset now, int maxAttempts, TimeSpan lockFor, CancellationToken ct)
    {
        var lockUntil = (DateTimeOffset?)(now + lockFor);

        // Both SET expressions read the row as it was before this UPDATE, so they agree on the new count.
        await db.Users
            .Where(u => u.Id == userId && (u.LockedUntil == null || u.LockedUntil <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.FailedLoginCount, u => (u.LockedUntil == null ? u.FailedLoginCount : 0) + 1)
                .SetProperty(u => u.LockedUntil,
                    u => (u.LockedUntil == null ? u.FailedLoginCount : 0) + 1 >= maxAttempts ? lockUntil : null)
                .SetProperty(u => u.UpdatedAt, now), ct);

        var until = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.LockedUntil).SingleAsync(ct);
        return until > now ? until : null;
    }

    public async Task<bool> TryAddAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }
}
