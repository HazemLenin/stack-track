namespace StackTrack.Domain.Entities;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Set by the SaveChanges interceptor only.
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
