namespace StackTrack.Application.Boards;

// Marker interfaces: a request that implements one is checked by BoardAccessBehavior before its handler runs.
public interface IBoardScoped { Guid BoardId { get; } }

// Commands that change data. An Admin with read-only access to the board is refused these.
public interface IWriteRequest;
