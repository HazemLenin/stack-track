namespace StackTrack.Domain.Entities;

public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
