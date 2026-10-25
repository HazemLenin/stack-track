namespace StackTrack.Application.Auth;

public record UserDto(Guid Id, string Email, string DisplayName, string Role);

public record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserDto User);

// The refresh token travels only in an HttpOnly cookie, so it is kept out of AuthResponse.
public record IssuedTokens(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
