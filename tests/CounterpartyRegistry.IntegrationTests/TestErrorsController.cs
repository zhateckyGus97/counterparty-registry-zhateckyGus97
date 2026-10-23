using Microsoft.AspNetCore.Mvc;

namespace CounterpartyRegistry.IntegrationTests;

// Discovered only by ApiFactory. These routes are not part of the application.
[ApiController]
[Route("__tests/errors")]
public sealed class TestErrorsController : ControllerBase
{
    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException("Internal implementation details");

    [HttpGet("argument")]
    public IActionResult Argument() => throw new ArgumentException("Internal implementation details");
}
