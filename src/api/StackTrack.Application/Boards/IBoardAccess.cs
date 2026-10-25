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
}
