using AutoParts.Api.Services.ClientApi;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

[ApiController]
[Route("bff/otp")]
public class BffOtpController : ControllerBase
{
    private readonly IOtpApiClient _otpApi;
    public BffOtpController(IOtpApiClient otpApi) { _otpApi = otpApi; }

    public class ResendRequest { public string PrescriptionNo { get; set; } }

    [HttpPost("resend")]
    public async Task<IActionResult> Resend([FromBody] ResendRequest req)
    {
        var otp = new Random().Next(100000, 999999).ToString();
        var payload = new { PrescriptionNo = req.PrescriptionNo, OTP = otp };
        var resp = await _otpApi.ResendAsync(payload, default);
        var msg = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, new { success = false, message = msg });
        try
        {
            var json = await resp.Content.ReadFromJsonAsync<dynamic>();
            bool ok = (bool?)json?.success ?? true;
            string? m = (string?)json?.message ?? "OTP resent successfully";
            return Ok(new { success = ok, message = m });
        }
        catch
        {
            return Ok(new { success = true, message = "OTP resent successfully" });
        }
    }
}
