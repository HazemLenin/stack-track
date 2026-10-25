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

    public async Task<Guid?> BoardIdOfListAsync(Guid listId, CancellationToken ct) =>
        await db.BoardLists.Where(l => l.Id == listId).Select(l => (Guid?)l.BoardId).FirstOrDefaultAsync(ct);

    public async Task<Guid?> BoardIdOfCardAsync(Guid cardId, CancellationToken ct) =>
        await db.Cards.Where(c => c.Id == cardId)
            .Join(db.BoardLists, c => c.ListId, l => l.Id, (c, l) => (Guid?)l.BoardId)
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> BoardIdOfLabelAsync(Guid labelId, CancellationToken ct) =>
        await db.Labels.Where(l => l.Id == labelId).Select(l => (Guid?)l.BoardId).FirstOrDefaultAsync(ct);

    public async Task<Guid?> BoardIdOfCommentAsync(Guid commentId, CancellationToken ct) =>
        await db.Comments.Where(m => m.Id == commentId)
            .Join(db.Cards, m => m.CardId, c => c.Id, (m, c) => c.ListId)
            .Join(db.BoardLists, listId => listId, l => l.Id, (_, l) => (Guid?)l.BoardId)
            .FirstOrDefaultAsync(ct);
}
