using StackTrack.Domain.Entities;

namespace StackTrack.Application.Auth;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    // Returns false when the email unique index rejects the insert (lost a race).
    Task<bool> TryAddAsync(User user, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    // Creates the access token and a stored (hashed) refresh token for the user.
    Task<IssuedTokens> IssueAsync(User user, CancellationToken ct);

    // Revokes the refresh token if it exists. Unknown tokens are ignored.
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}
