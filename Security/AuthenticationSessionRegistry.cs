using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Security;

/// <summary>
/// Keeps authentication-session revocation outside mutable ASP.NET session state.
/// </summary>
public sealed class AuthenticationSessionRegistry
{
    private const string CacheKeyPrefix = "authentication-session:active:";
    private readonly IMemoryCache _cache;

    public AuthenticationSessionRegistry(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Register(string sessionId, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        _cache.Set(CacheKeyPrefix + sessionId, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime
        });
    }

    public bool IsActive(string? sessionId)
    {
        return !string.IsNullOrWhiteSpace(sessionId) &&
            _cache.TryGetValue(CacheKeyPrefix + sessionId, out bool active) && active;
    }

    public void Revoke(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            _cache.Remove(CacheKeyPrefix + sessionId);
    }
}
