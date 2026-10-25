using StackTrack.Domain.Entities;

namespace StackTrack.Application.Auth;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    // Returns false when the email unique index rejects the insert (lost a race).
    Task<bool> TryAddAsync(User user, CancellationToken ct);

    // Tracked entity: changes are persisted by SaveAsync.
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);

    // Spends the same time as Verify, used when the email is unknown so timing does not reveal it.
    void VerifyDummy(string password);
}

public interface ITokenService
{
    // Creates the access token and a stored (hashed) refresh token for the user.
    Task<IssuedTokens> IssueAsync(User user, CancellationToken ct);

    // Swaps a valid refresh token for a new pair and revokes the old one. Returns null when the token is
    // unknown, expired, revoked, already used, or its user is inactive. Replaying a used token revokes
    // every token issued after it.
    Task<IssuedTokens?> RotateAsync(string refreshToken, CancellationToken ct);

    // Revokes the refresh token if it exists. Unknown tokens are ignored.
    Task RevokeAsync(string refreshToken, CancellationToken ct);
}
