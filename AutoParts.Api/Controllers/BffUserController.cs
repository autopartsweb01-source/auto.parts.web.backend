using AutoParts.Api.Services.ClientApi;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("bff/user")]
public class BffUserController : ControllerBase
{
    private readonly IUserApiClient _user;

    public BffUserController(IUserApiClient user)
    {
        _user = user;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var upstream = await _user.GetProfileAsync(ct);
        var content = await upstream.Content.ReadAsStringAsync(ct);
        if (!upstream.IsSuccessStatusCode) return StatusCode((int)upstream.StatusCode, content);
        return Content(content, upstream.Content.Headers.ContentType?.ToString() ?? "application/json");
    }

    [HttpGet("profile/{email}")]
    public async Task<IActionResult> ProfileByEmail(string email, CancellationToken ct)
    {
        var upstream = await _user.GetProfileByEmailAsync(email, ct);
        var content = await upstream.Content.ReadAsStringAsync(ct);
        if (!upstream.IsSuccessStatusCode) return StatusCode((int)upstream.StatusCode, content);
        return Content(content, upstream.Content.Headers.ContentType?.ToString() ?? "application/json");
    }
}
