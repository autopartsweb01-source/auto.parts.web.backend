using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoParts.Api.Services.ClientApi;

public interface IAuthApiClient
{
    Task<LoginResult> LoginAsync(ClientAuthLoginRequest request, CancellationToken ct);
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct);
    Task<RegisterResult> RegisterAsync(ClientAuthRegisterRequest request, CancellationToken ct);
    Task<ApiOpResult> ForgotPasswordAsync(ClientForgotPasswordRequest request, CancellationToken ct);
    Task<ApiOpResult> ResetPasswordAsync(ClientResetPasswordRequest request, CancellationToken ct);
}

public sealed class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<LoginResult> LoginAsync(ClientAuthLoginRequest request, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("auth/login", request, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return new LoginResult(false, null, null, null, null, body);

        JsonDocument? docParsed = null;
        try
        {
            docParsed = JsonDocument.Parse(body);
        }
        catch
        {
            return new LoginResult(false, null, null, null, null, body);
        }
        using var doc = docParsed;
        var root = doc.RootElement;
        var success = TryGetBool(root, "success") ?? true;

        // Attempt robust token extraction (root or nested under 'data', arrays, etc.)
        var access = TryFindStringByKeyContains(root, new[] { "accessToken", "access_token", "jwt", "token", "bearer" });
        var refresh = TryFindStringByKeyContains(root, new[] { "refreshToken", "refresh_token" });

        var accessExp = TryGetDateTime(root, "accessExpiresAt") ??
                        TryGetExpiresIn(root, "accessExpiresIn", TimeSpan.FromHours(1)) ??
                        DateTimeOffset.UtcNow.AddHours(1);
        var refreshExp = TryGetDateTime(root, "refreshExpiresAt") ??
                         TryGetExpiresIn(root, "refreshExpiresIn", TimeSpan.FromDays(1)) ??
                         DateTimeOffset.UtcNow.AddDays(1);

        return new LoginResult(success, access, accessExp, refresh, refreshExp, success ? null : body);
    }

    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var payload = new { refreshToken };
        using var res = await _http.PostAsJsonAsync("auth/refresh-token", payload, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return new RefreshResult(false, null, default, null, default, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = TryGetString(root, "accessToken") ?? TryGetString(root, "token");
        var refresh = TryGetString(root, "refreshToken") ?? refreshToken;
        var accessExp = TryGetDateTime(root, "accessExpiresAt") ?? DateTimeOffset.UtcNow.AddHours(1);
        var refreshExp = TryGetDateTime(root, "refreshExpiresAt") ?? DateTimeOffset.UtcNow.AddDays(1);
        return new RefreshResult(true, access, accessExp, refresh, refreshExp, null);
    }

    public async Task<RegisterResult> RegisterAsync(ClientAuthRegisterRequest request, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("auth/register", request, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return new RegisterResult(false, null, null, null, null, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = TryGetString(root, "accessToken") ?? TryGetString(root, "token");
        var refresh = TryGetString(root, "refreshToken");
        var accessExp = TryGetDateTime(root, "accessExpiresAt") ?? DateTimeOffset.UtcNow.AddHours(1);
        var refreshExp = TryGetDateTime(root, "refreshExpiresAt") ?? DateTimeOffset.UtcNow.AddDays(1);
        // Some backends don't return tokens on register; tolerate that.
        if (access is null || refresh is null)
            return new RegisterResult(true, null, null, null, null, null);
        return new RegisterResult(true, access, accessExp, refresh, refreshExp, null);
    }

    public async Task<ApiOpResult> ForgotPasswordAsync(ClientForgotPasswordRequest request, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("forgot-password", request, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        return new ApiOpResult(res.IsSuccessStatusCode, body);
    }

    public async Task<ApiOpResult> ResetPasswordAsync(ClientResetPasswordRequest request, CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("reset-password", request, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        return new ApiOpResult(res.IsSuccessStatusCode, body);
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.String) return p.GetString();
        }
        return null;
    }

    private static DateTimeOffset? TryGetDateTime(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(p.GetString(), out var dto)) return dto;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var seconds)) return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        return null;
    }

    private static DateTimeOffset? TryGetExpiresIn(JsonElement root, string name, TimeSpan fallback)
    {
        if (root.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var seconds))
                return DateTimeOffset.UtcNow.AddSeconds(seconds);
        }
        return null;
    }

    private static bool? TryGetBool(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.True) return true;
            if (p.ValueKind == JsonValueKind.False) return false;
            if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b)) return b;
        }
        return null;
    }

    private static string? TryFindStringByKeyContains(JsonElement element, string[] keyContains)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var key = prop.Name;
                    foreach (var kc in keyContains)
                    {
                        if (key.Contains(kc, StringComparison.OrdinalIgnoreCase))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                return prop.Value.GetString();
                            // Sometimes nested token object: keep searching within
                            var nested = TryFindStringByKeyContains(prop.Value, keyContains);
                            if (nested is not null) return nested;
                        }
                    }
                    var deeper = TryFindStringByKeyContains(prop.Value, keyContains);
                    if (deeper is not null) return deeper;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = TryFindStringByKeyContains(item, keyContains);
                    if (found is not null) return found;
                }
                break;
        }
        return null;
    }
}

public record ClientAuthLoginRequest(string Email, string Password);
public record ClientAuthRegisterRequest(string Name, string Email, string Password, string ConfirmPassword, string Mobile, string? Location);
public record ClientForgotPasswordRequest(string Email);
public record ClientResetPasswordRequest(string Email, string Token, string NewPassword);
public record LoginResult(bool Success, string? AccessToken, DateTimeOffset? AccessExpiresAt, string? RefreshToken, DateTimeOffset? RefreshExpiresAt, string? Error);
public record RefreshResult(bool Success, string? AccessToken, DateTimeOffset AccessExpiresAt, string? RefreshToken, DateTimeOffset RefreshExpiresAt, string? Error);
public record RegisterResult(bool Success, string? AccessToken, DateTimeOffset? AccessExpiresAt, string? RefreshToken, DateTimeOffset? RefreshExpiresAt, string? Error);
public record ApiOpResult(bool Success, string? RawBody);
