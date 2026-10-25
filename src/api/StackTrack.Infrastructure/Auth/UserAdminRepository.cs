using Microsoft.EntityFrameworkCore;
using StackTrack.Application.Admin.Users;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure.Auth;

public class UserAdminRepository(AppDbContext db) : IUserAdminRepository
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
}
