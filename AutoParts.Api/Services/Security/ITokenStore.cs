using System.Collections.Concurrent;

namespace AutoParts.Api.Services.Security;

public record TokenPair(string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt);

public interface ITokenStore
{
    Task SetAsync(string sessionId, TokenPair tokens, CancellationToken ct = default);
    Task<TokenPair?> GetAsync(string sessionId, CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);
}

public class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, TokenPair> _store = new();

    public Task SetAsync(string sessionId, TokenPair tokens, CancellationToken ct = default)
    {
        _store[sessionId] = tokens;
        return Task.CompletedTask;
    }

    public Task<TokenPair?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        _store.TryGetValue(sessionId, out var tokens);
        return Task.FromResult<TokenPair?>(tokens);
    }

    public Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        _store.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
