namespace StackTrack.Infrastructure.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
