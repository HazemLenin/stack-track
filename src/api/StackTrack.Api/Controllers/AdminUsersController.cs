using MediatR;
using Microsoft.AspNetCore.Mvc;
using StackTrack.Application.Admin.Users;
using StackTrack.Application.Admin.Users.ChangeUserRole;
using StackTrack.Application.Admin.Users.DeactivateUser;
using StackTrack.Application.Admin.Users.ListUsers;
using StackTrack.Application.Admin.Users.ReactivateUser;

namespace StackTrack.Api.Controllers;

[Route("admin/users")]
public class AdminUsersController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<UserPage> List([FromQuery] ListUsersQuery query, CancellationToken ct) => sender.Send(query, ct);

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeactivateUserCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await sender.Send(new ReactivateUserCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest body, CancellationToken ct)
    {
        await sender.Send(new ChangeUserRoleCommand(id, body.Role), ct);
        return NoContent();
    }

    public record ChangeRoleRequest(string Role);
}
