using MediatR;
using Microsoft.AspNetCore.Mvc;
using StackTrack.Application.Auth;
using StackTrack.Application.Auth.Register;
using StackTrack.Application.Auth.SignOut;

namespace StackTrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    public const string RefreshCookie = "refresh_token";
    const string CookiePath = "/api/auth";

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCommand command, CancellationToken ct)
    {
        var tokens = await sender.Send(command, ct);
        SetRefreshCookie(tokens);
        return StatusCode(StatusCodes.Status201Created, tokens.Response);
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        await sender.Send(new SignOutCommand(Request.Cookies[RefreshCookie]), ct);
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = CookiePath, Secure = true, SameSite = SameSiteMode.Strict, HttpOnly = true });
        return NoContent();
    }

    void SetRefreshCookie(IssuedTokens tokens) =>
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = tokens.RefreshTokenExpiresAt,
        });
}
