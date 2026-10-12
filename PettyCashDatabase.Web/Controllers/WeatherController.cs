using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Web.Controllers;

[ApiController]
[Route("api/weather")]
[Authorize(Policy = Permissions.WeatherView)]
public sealed class WeatherController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet]
    public ActionResult<IEnumerable<WeatherForecastDto>> Get()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var forecasts = Enumerable.Range(1, 5).Select(i => new WeatherForecastDto
        {
            Date = start.AddDays(i),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });

        return Ok(forecasts);
    }
}

public sealed class WeatherForecastDto
{
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
