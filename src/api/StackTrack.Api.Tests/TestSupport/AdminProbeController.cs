using Microsoft.AspNetCore.Mvc;
using StackTrack.Api.Controllers;

namespace StackTrack.Api.Tests.TestSupport;

// Stands in for a real admin controller: protection must come from AdminControllerBase alone.
public class AdminProbeController : AdminControllerBase
{
    [HttpGet("probe")]
    public IActionResult Probe() => Ok();
}
