using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackTrack.Application.Auth;
using StackTrack.Application.Auth.Refresh;
using StackTrack.Application.Auth.Register;
using StackTrack.Application.Auth.SignIn;
using StackTrack.Application.Auth.SignOut;
using StackTrack.Infrastructure.Auth;

namespace StackTrack.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(ISender sender, IOptions<JwtOptions> jwt) : ControllerBase
{
    public const string RefreshCookie = "refresh_token";

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCommand command, CancellationToken ct)
    {
        var tokens = await sender.Send(command, ct);
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAt));
        return StatusCode(StatusCodes.Status201Created, tokens.Response);
    }

    [HttpPost("sign-in")]
    public async Task<ActionResult<AuthResponse>> SignIn(SignInCommand command, CancellationToken ct)
    {
        var tokens = await sender.Send(command, ct);
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAt));
        return tokens.Response;
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var tokens = await sender.Send(new RefreshTokenCommand(Request.Cookies[RefreshCookie] ?? ""), ct);
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAt));
        return tokens.Response;
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        await sender.Send(new SignOutCommand(Request.Cookies[RefreshCookie]), ct);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(null));
        return NoContent();
    }

    CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = jwt.Value.RefreshCookiePath,
        Expires = expires,
    };
}
