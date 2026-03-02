using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AutoParts.Api.Services.ClientApi;
using AutoParts.Api.Services.Security;
using Microsoft.AspNetCore.Http;

namespace AutoParts.Api.Infrastructure.Http;

public class AuthForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenStore _tokenStore;
    private readonly IAuthApiClient _authApiClient;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthForwardingHandler(IHttpContextAccessor httpContextAccessor, ITokenStore tokenStore, IAuthApiClient authApiClient)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenStore = tokenStore;
        _authApiClient = authApiClient;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sid = _httpContextAccessor.HttpContext?.Request.Cookies["sid"];
        if (string.IsNullOrEmpty(sid))
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var tokens = await _tokenStore.GetAsync(sid, cancellationToken);
        if (tokens is null)
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var latest = await _tokenStore.GetAsync(sid, cancellationToken);
            if (latest is null)
                return response;

            var refreshed = await _authApiClient.RefreshAsync(latest.RefreshToken, cancellationToken);
            if (!refreshed.Success)
            {
                await _tokenStore.RemoveAsync(sid, cancellationToken);
                return response;
            }

            var updated = new TokenPair(refreshed.AccessToken, refreshed.AccessExpiresAt, refreshed.RefreshToken, refreshed.RefreshExpiresAt);
            await _tokenStore.SetAsync(sid, updated, cancellationToken);

            var retry = request.Clone();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", updated.AccessToken);
            return await base.SendAsync(retry, cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
