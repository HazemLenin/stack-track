using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackTrack.Infrastructure;

namespace StackTrack.Api.Controllers;

// Every admin controller derives from this, so a new admin endpoint is Admin-only by default.
[ApiController]
[Authorize(Policy = DependencyInjection.AdminPolicy)]
[Route("admin")]
public abstract class AdminControllerBase : ControllerBase;
