namespace StackTrack.Application.Boards;

// Marker interfaces: a request that implements one is checked by BoardAccessBehavior before its handler runs.
public interface IBoardScoped { Guid BoardId { get; } }

public interface IListScoped { Guid ListId { get; } }
public interface ICardScoped { Guid CardId { get; } }
public interface ILabelScoped { Guid LabelId { get; } }
public interface ICommentScoped { Guid CommentId { get; } }

// Commands that change data. An Admin with read-only access to the board is refused these.
public interface IWriteRequest;
