using Microsoft.AspNetCore.Mvc;
using UptimeDaddy.API.Models;

[ApiController]
[Route("api/[controller]")]
public class WebsitesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new[]
        {
            new Website { Id = 1, Url = "https://google.com", Name = "Google" }
        });
    }
}