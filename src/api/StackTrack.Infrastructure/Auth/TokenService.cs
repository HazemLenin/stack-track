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
