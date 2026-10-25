using StackTrack.Domain.Entities;

namespace StackTrack.Application.Admin.Users;

public record UserListItem(Guid Id, string DisplayName, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);

public record UserPage(IReadOnlyList<UserListItem> Items, int Page, int PageSize, int TotalCount);

public enum UserChange
{
    Done,
    NotFound,
    LastAdmin,
}

public interface IUserAdminRepository
{
    // Search is a case-insensitive substring match on display name and email. Sort is "name" or "created".
    Task<UserPage> ListAsync(string? search, string sort, string dir, int page, int pageSize, CancellationToken ct);

    // Deactivating also revokes the user's refresh tokens. Setting the state a user already has is a no-op.
    // Removing the last active admin is refused, even when two requests race.
    Task<UserChange> SetActiveAsync(Guid id, bool isActive, CancellationToken ct);

    Task<UserChange> SetRoleAsync(Guid id, UserRole role, CancellationToken ct);
}

public static class UserChangeExtensions
{
    public const string LastAdminMessage = "There must be at least one active administrator";

    // Maps the outcome to the shared exceptions so each handler stays one line.
    public static void ThrowIfRefused(this UserChange change)
    {
        if (change == UserChange.NotFound) throw new Errors.NotFoundException("User not found");
        if (change == UserChange.LastAdmin) throw new Errors.ConflictException(LastAdminMessage);
    }
}
