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
}
