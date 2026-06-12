using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Controllers;

namespace UrlShortener.Api.Tests.Controllers;

public class HealthControllerTests
{
    private readonly HealthController _controller = new();

    [Fact]
    public void Health_ReturnsOk()
    {
        var result = _controller.Health();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }
}
