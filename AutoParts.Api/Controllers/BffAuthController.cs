using AutoParts.Api.Services.ClientApi;
using AutoParts.Api.Services.Security;
using Microsoft.AspNetCore.Mvc;
using AutoParts.Api.DTO;
using System.Text.Json;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("bff/auth")]
public class BffAuthController : ControllerBase
{
    private readonly IAuthApiClient _auth;
    private readonly ITokenStore _tokens;
    private readonly IUserApiClient _userApi;

    public BffAuthController(IAuthApiClient auth, ITokenStore tokens, IUserApiClient userApi)
    {
        _auth = auth;
        _tokens = tokens;
        _userApi = userApi;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto, CancellationToken ct)
    {
        var res = await _auth.RegisterAsync(
            new ClientAuthRegisterRequest(dto.Name, dto.Email, dto.Password, dto.ConfirmPassword, dto.Mobile, dto.Location),
            ct);

        if (!res.Success)
            return Problem(statusCode: 400, title: "Registration failed");

        if (res.AccessToken is not null && res.RefreshToken is not null && res.AccessExpiresAt is not null && res.RefreshExpiresAt is not null)
        {
            var sid = Guid.NewGuid().ToString("N");
            await _tokens.SetAsync(sid, new TokenPair(res.AccessToken, res.AccessExpiresAt.Value, res.RefreshToken, res.RefreshExpiresAt.Value), ct);
            Response.Cookies.Append("sid", sid, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = res.RefreshExpiresAt
            });
        }

        return Ok(new { ok = true });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest dto, CancellationToken ct)
    {
        var res = await _auth.ForgotPasswordAsync(new ClientForgotPasswordRequest(dto.Email), ct);
        if (!res.Success) return Problem(statusCode: 400, title: "Forgot password failed");
        return Ok(new { ok = true });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest dto, CancellationToken ct)
    {
        var res = await _auth.ResetPasswordAsync(new ClientResetPasswordRequest(dto.Email, dto.Token, dto.NewPassword), ct);
        if (!res.Success) return Problem(statusCode: 400, title: "Reset password failed");
        return Ok(new { ok = true });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(new ClientAuthLoginRequest(dto.Email, dto.Password), ct);
        if (!res.Success || res.AccessToken is null)
            return Ok(new { success = false, message = "Invalid credentials" });

        var sid = Guid.NewGuid().ToString("N");
        var accessExp = res.AccessExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
        var refreshTok = res.RefreshToken ?? "";
        var refreshExp = res.RefreshExpiresAt ?? DateTimeOffset.UtcNow.AddDays(1);
        await _tokens.SetAsync(sid, new TokenPair(res.AccessToken, accessExp, refreshTok, refreshExp), ct);
        Response.Cookies.Append("sid", sid, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = refreshExp
        });

        string email = dto.Email;
        string? name = null;
        string? mobile = null;
        string? location = null;
        try
        {
            var upstream = await _userApi.GetProfileAsync(ct);
            var content = await upstream.Content.ReadAsStringAsync(ct);
            var root = JsonDocument.Parse(content).RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
                root = data;
            email = TryProp(root, "email", "Email") ?? email;
            name = TryProp(root, "name", "Name", "fullName", "FullName");
            mobile = TryProp(root, "mobile", "Mobile", "phone", "Phone");
            location = TryProp(root, "location", "Location", "address", "Address");
        }
        catch { }

        return Ok(new
        {
            success = true,
            message = "Login successful",
            data = new
            {
                token = res.AccessToken,
                accessToken = res.AccessToken,
                refreshToken = string.IsNullOrEmpty(refreshTok) ? null : refreshTok,
                email,
                name,
                mobile,
                location,
                expiration = accessExp.UtcDateTime.ToString("o")
            }
        });
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken ct)
    {
        var sid = Request.Cookies["sid"];
        if (string.IsNullOrEmpty(sid)) return Unauthorized();
        var pair = await _tokens.GetAsync(sid, ct);
        if (pair is null) return Unauthorized();
        return Ok(new { ok = true });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sid = Request.Cookies["sid"];
        if (!string.IsNullOrEmpty(sid))
        {
            await _tokens.RemoveAsync(sid, ct);
            Response.Cookies.Append("sid", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UnixEpoch
            });
        }
        return NoContent();
    }

    private static string? TryProp(JsonElement obj, params string[] names)
    {
        foreach (var n in names)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }
        return null;
    }
}
