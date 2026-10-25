using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StackTrack.Application.Auth;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Auth;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // The "sub" claim is kept as is because bearer claim mapping is off.
    public Guid Id => Guid.Parse(User.FindFirst("sub")?.Value ?? throw new InvalidOperationException("No signed-in user."));

    public bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    ClaimsPrincipal User => accessor.HttpContext?.User ?? throw new InvalidOperationException("No HTTP request.");
}
