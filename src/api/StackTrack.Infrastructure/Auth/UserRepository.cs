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
