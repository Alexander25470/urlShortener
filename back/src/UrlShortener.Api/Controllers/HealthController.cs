using Microsoft.AspNetCore.Mvc;

namespace UrlShortener.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Health check endpoint for Docker healthcheck and load balancer probes.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "healthy" });
}
