using System.Text.Json;
using AutoParts.Api.DTO;
using AutoParts.Api.Services.ClientApi;
using AutoParts.Api.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly IAuthApiClient _auth;
    private readonly IUserApiClient _userApi;
    private readonly ITokenStore _tokens;

    public UsersController(IAuthApiClient auth, IUserApiClient userApi, ITokenStore tokens)
    {
        _auth = auth;
        _userApi = userApi;
        _tokens = tokens;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto, CancellationToken ct)
    {
        var res = await _auth.LoginAsync(new ClientAuthLoginRequest(dto.Email, dto.Password), ct);
        if (!res.Success || res.AccessToken is null)
            return Ok(new { success = false, message = "Invalid credentials" });

        // Create session and cookie
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

        // Fetch user profile from upstream and include in response
        object? userObj = null;
        try
        {
            var upstream = await _userApi.GetProfileAsync(ct);
            var content = await upstream.Content.ReadAsStringAsync(ct);
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(content);
                userObj = NormalizeUser(root);
            }
            catch
            {
                userObj = new { email = dto.Email };
            }
        }
        catch
        {
            userObj = new { email = dto.Email };
        }

        return Ok(new
        {
            success = true,
            message = "Login successful",
            token = res.AccessToken,
            refreshToken = string.IsNullOrEmpty(refreshTok) ? null : refreshTok,
            user = userObj
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto, CancellationToken ct)
    {
        var res = await _auth.RegisterAsync(
            new ClientAuthRegisterRequest(dto.Name, dto.Email, dto.Password, dto.ConfirmPassword, dto.Mobile, dto.Location),
            ct);

        // If backend does not return tokens on register, still report success but without tokens
        bool tokensReturned = res.AccessToken is not null;

        if (!res.Success)
            return Ok(new { success = false, message = "Registration failed" });

        object? userObj = new { email = dto.Email, name = dto.Name, mobile = dto.Mobile, location = dto.Location };

        if (tokensReturned)
        {
            var sid = Guid.NewGuid().ToString("N");
            var accessExp = res.AccessExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1);
            var refreshTok = res.RefreshToken ?? "";
            var refreshExp = res.RefreshExpiresAt ?? DateTimeOffset.UtcNow.AddDays(1);
            await _tokens.SetAsync(sid, new TokenPair(res.AccessToken!, accessExp, refreshTok, refreshExp), ct);
            Response.Cookies.Append("sid", sid, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = refreshExp
            });

            try
            {
                var upstream = await _userApi.GetProfileAsync(ct);
                var content = await upstream.Content.ReadAsStringAsync(ct);
                var root = JsonSerializer.Deserialize<JsonElement>(content);
                userObj = NormalizeUser(root);
            }
            catch { /* ignore and keep fallback */ }
        }

        return Ok(new
        {
            success = true,
            message = "Registration successful",
            token = tokensReturned ? res.AccessToken : null,
            refreshToken = tokensReturned ? res.RefreshToken : null,
            user = userObj
        });
    }

    private static object NormalizeUser(JsonElement root)
    {
        // Upstream may wrap the user in { data: { ... } } or return directly
        var userEl = TryGetChild(root, new[] { "data", "user", "profile" }) ?? root;
        string? email = TryFindString(userEl, new[] { "email", "Email" });
        string? name = TryFindString(userEl, new[] { "name", "Name", "fullName", "FullName" });
        string? mobile = TryFindString(userEl, new[] { "mobile", "Mobile", "phone", "Phone" });
        string? location = TryFindString(userEl, new[] { "location", "Location", "address", "Address" });
        string? role = TryFindString(userEl, new[] { "role", "Role" });
        int? id = TryFindInt(userEl, new[] { "id", "Id", "userId", "UserId" });

        return new
        {
            id,
            email,
            name,
            mobile,
            location,
            role
        };
    }

    private static JsonElement? TryGetChild(JsonElement root, string[] pathCandidates)
    {
        foreach (var key in pathCandidates)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var child))
                return child;
        }
        return null;
    }

    private static string? TryFindString(JsonElement element, string[] keys)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (keys.Any(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String) return prop.Value.GetString();
                }
            }
        }
        return null;
    }

    private static int? TryFindInt(JsonElement element, string[] keys)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (keys.Any(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var v)) return v;
                    if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out var vs)) return vs;
                }
            }
        }
        return null;
    }
}
