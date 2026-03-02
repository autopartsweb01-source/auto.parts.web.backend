using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoParts.Api.Services.ClientApi;

public interface IUserApiClient
{
    Task<HttpResponseMessage> GetProfileAsync(CancellationToken ct);
    Task<HttpResponseMessage> GetProfileByEmailAsync(string email, CancellationToken ct);
}

public sealed class UserApiClient : IUserApiClient
{
    private readonly HttpClient _http;

    public UserApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<HttpResponseMessage> GetProfileAsync(CancellationToken ct)
        => _http.GetAsync("user/profile", ct);

    public Task<HttpResponseMessage> GetProfileByEmailAsync(string email, CancellationToken ct)
        => _http.GetAsync($"user/profile/{Uri.EscapeDataString(email)}", ct);
}
