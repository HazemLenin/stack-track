namespace StackTrack.Infrastructure.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;

    // The browser matches cookie paths against the public URL, where a proxy serves the API under /api.
    public string RefreshCookiePath { get; set; } = "/api/auth";
}
