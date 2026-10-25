namespace StackTrack.Domain.Entities;

public class Board : Entity
{
    public Guid OwnerId { get; set; }
    public required string Name { get; set; }
    public bool IsArchived { get; set; }
}

public class BoardMember : Entity
{
    public Guid BoardId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public class BoardList : Entity
{
    public Guid BoardId { get; set; }
    public required string Name { get; set; }
    public double Position { get; set; }

    // Postgres xmin, optimistic concurrency token.
    public uint Version { get; set; }
}

public class Label : Entity
{
    public Guid BoardId { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
}
