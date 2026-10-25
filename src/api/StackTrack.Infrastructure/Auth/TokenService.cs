using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackTrack.Application.Auth;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Persistence;

namespace StackTrack.Infrastructure.Auth;

public class TokenService(AppDbContext db, IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    readonly JwtOptions _jwt = options.Value;

    public async Task<IssuedTokens> IssueAsync(User user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var accessExpires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var accessToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = accessExpires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("name", user.DisplayName),
                new Claim("role", user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)), SecurityAlgorithms.HmacSha256),
        });

        var refreshToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var refreshExpires = now.AddDays(_jwt.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(refreshToken), ExpiresAt = refreshExpires });
        await db.SaveChangesAsync(ct);

        var dto = new UserDto(user.Id, user.Email, user.DisplayName, user.Role.ToString());
        return new IssuedTokens(new AuthResponse(accessToken, accessExpires, dto), refreshToken, refreshExpires);
    }

    public async Task<IssuedTokens?> RotateAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;

        var now = clock.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return null;

        if (stored.IsRevoked)
        {
            // A used token is being replayed: treat the whole chain after it as stolen.
            await RevokeChainAfterAsync(stored, ct);
            return null;
        }

        if (stored.ExpiresAt <= now) return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive) return null;

        // Atomic claim: of two concurrent requests with the same token, only one updates a row.
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true).SetProperty(t => t.UpdatedAt, now), ct);
        if (claimed == 0) return null;

        var issued = await IssueAsync(user, ct);

        var newHash = Hash(issued.RefreshToken);
        var newId = await db.RefreshTokens.Where(t => t.TokenHash == newHash).Select(t => t.Id).SingleAsync(ct);
        await db.RefreshTokens.Where(t => t.Id == stored.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByTokenId, newId), ct);
        return issued;
    }

    async Task RevokeChainAfterAsync(RefreshToken used, CancellationToken ct)
    {
        var nextId = used.ReplacedByTokenId;
        while (nextId is not null)
        {
            var next = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == nextId, ct);
            if (next is null) break;
            next.IsRevoked = true;
            nextId = next.ReplacedByTokenId;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.IsRevoked) return;

        stored.IsRevoked = true;
        await db.SaveChangesAsync(ct);
    }

    // Hex SHA-256 of the raw token: high-entropy random value, so a fast hash is enough.
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
