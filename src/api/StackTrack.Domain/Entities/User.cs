namespace StackTrack.Domain.Entities;

public enum UserRole
{
    Admin,
    Member,
}

public class User : Entity
{
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;
    public bool IsActive { get; set; } = true;

    // Consecutive wrong passwords; 5 in a row sets LockedUntil.
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
