using Microsoft.AspNetCore.Http;
using StackTrack.Application.Auth;
using StackTrack.Domain.Entities;

namespace StackTrack.Infrastructure.Auth;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // The "sub" claim is kept as is because bearer claim mapping is off.
    public Guid Id => Guid.Parse(accessor.HttpContext!.User.FindFirst("sub")!.Value);

    public bool IsAdmin => accessor.HttpContext!.User.IsInRole(nameof(UserRole.Admin));
}
