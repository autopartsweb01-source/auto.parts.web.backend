using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoParts.Api.Services.ClientApi;

public interface IOtpApiClient
{
    Task<HttpResponseMessage> VerifyAsync(object payload, CancellationToken ct);
    Task<HttpResponseMessage> ResendAsync(object payload, CancellationToken ct);
}

public sealed class OtpApiClient : IOtpApiClient
{
    private readonly HttpClient _http;

    public OtpApiClient(HttpClient http)
    {
        _http = http;
    }

    public Task<HttpResponseMessage> VerifyAsync(object payload, CancellationToken ct)
        => _http.PostAsJsonAsync("Otp/verify", payload, ct);

    public Task<HttpResponseMessage> ResendAsync(object payload, CancellationToken ct)
        => _http.PostAsJsonAsync("Otp/resend", payload, ct);
}
