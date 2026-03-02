using AutoParts.Api.Services.ClientApi;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("bff/otp")]
public class BffOtpController : ControllerBase
{
    private readonly IOtpApiClient _otp;

    public BffOtpController(IOtpApiClient otp)
    {
        _otp = otp;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] object payload, CancellationToken ct)
    {
        var upstream = await _otp.VerifyAsync(payload, ct);
        var content = await upstream.Content.ReadAsStringAsync(ct);
        if (!upstream.IsSuccessStatusCode) return StatusCode((int)upstream.StatusCode, content);
        return Content(content, upstream.Content.Headers.ContentType?.ToString() ?? "application/json");
    }

    [HttpPost("resend")]
    public async Task<IActionResult> Resend([FromBody] object payload, CancellationToken ct)
    {
        var upstream = await _otp.ResendAsync(payload, ct);
        var content = await upstream.Content.ReadAsStringAsync(ct);
        if (!upstream.IsSuccessStatusCode) return StatusCode((int)upstream.StatusCode, content);
        return Content(content, upstream.Content.Headers.ContentType?.ToString() ?? "application/json");
    }
}
