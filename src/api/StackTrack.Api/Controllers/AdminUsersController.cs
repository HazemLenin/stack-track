using MediatR;
using Microsoft.AspNetCore.Mvc;
using StackTrack.Application.Admin.Users;
using StackTrack.Application.Admin.Users.ListUsers;

namespace StackTrack.Api.Controllers;

[Route("admin/users")]
public class AdminUsersController(ISender sender) : AdminControllerBase
{
    [HttpGet]
    public Task<UserPage> List([FromQuery] ListUsersQuery query, CancellationToken ct) => sender.Send(query, ct);
}
