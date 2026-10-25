namespace StackTrack.Domain.Entities;

public class Card : Entity
{
    public Guid ListId { get; set; }
    public required string Title { get; set; }
    public string Description { get; set; } = "";
    public double Position { get; set; }
    public DateTimeOffset? DueDate { get; set; }

    // Postgres xmin, optimistic concurrency token.
    public uint Version { get; set; }
}

public class CardAssignee : Entity
{
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
}

public class CardLabel : Entity
{
    public Guid CardId { get; set; }
    public Guid LabelId { get; set; }
}

public class Comment : Entity
{
    public Guid CardId { get; set; }
    public Guid AuthorId { get; set; }
    public required string Body { get; set; }
}
