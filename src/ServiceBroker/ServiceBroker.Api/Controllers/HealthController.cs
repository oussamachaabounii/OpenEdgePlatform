using Microsoft.AspNetCore.Mvc;

namespace OpenEdgePlatform.ServiceBroker.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "service-broker",
        timestamp = DateTimeOffset.UtcNow
    });
}
