using Microsoft.AspNetCore.Mvc;

namespace StackTrack.Api.Tests.TestSupport;

// No attributes: relies on the global fallback policy.
[ApiController]
[Route("test/protected")]
public class ProtectedController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
