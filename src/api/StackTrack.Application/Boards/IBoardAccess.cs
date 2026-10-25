namespace StackTrack.Application.Boards;

public enum BoardAccessLevel
{
    None,
    ReadOnly,
    Full,
}

public interface IBoardAccess
{
    // Owners and members get Full, an Admin who is not a member gets ReadOnly, everyone else (and an
    // unknown board) gets None.
    Task<BoardAccessLevel> GetAsync(Guid boardId, CancellationToken ct);

    // Owning board of a child item, or null when the item does not exist. One query each.
    Task<Guid?> BoardIdOfListAsync(Guid listId, CancellationToken ct);
    Task<Guid?> BoardIdOfCardAsync(Guid cardId, CancellationToken ct);
    Task<Guid?> BoardIdOfLabelAsync(Guid labelId, CancellationToken ct);
    Task<Guid?> BoardIdOfCommentAsync(Guid commentId, CancellationToken ct);
}
