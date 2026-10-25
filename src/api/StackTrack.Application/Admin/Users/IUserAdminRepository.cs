namespace StackTrack.Application.Admin.Users;

public record UserListItem(Guid Id, string DisplayName, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt);

public record UserPage(IReadOnlyList<UserListItem> Items, int Page, int PageSize, int TotalCount);

public interface IUserAdminRepository
{
    // Search is a case-insensitive substring match on display name and email. Sort is "name" or "created".
    Task<UserPage> ListAsync(string? search, string sort, string dir, int page, int pageSize, CancellationToken ct);
}
