using Microsoft.EntityFrameworkCore;
using StackTrack.Application.Auth;
using StackTrack.Application.Boards;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure.Boards;

public class BoardAccess(AppDbContext db, ICurrentUser user) : IBoardAccess
{
    public async Task<BoardAccessLevel> GetAsync(Guid boardId, CancellationToken ct)
    {
        var userId = user.Id;
        var isOwnerOrMember = await db.Boards
            .Where(b => b.Id == boardId)
            .Select(b => b.OwnerId == userId || db.BoardMembers.Any(m => m.BoardId == b.Id && m.UserId == userId))
            .Cast<bool?>()
            .FirstOrDefaultAsync(ct);

        if (isOwnerOrMember is null) return BoardAccessLevel.None;
        if (isOwnerOrMember.Value) return BoardAccessLevel.Full;
        return user.IsAdmin ? BoardAccessLevel.ReadOnly : BoardAccessLevel.None;
    }
}
