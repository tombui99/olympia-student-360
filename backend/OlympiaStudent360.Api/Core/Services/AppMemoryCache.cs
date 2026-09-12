using Microsoft.Extensions.Caching.Memory;

namespace OlympiaStudent360.Api.Core.Services;

public static class AppMemoryCache
{
    /// <summary>Adds small expiry jitter so popular dashboard keys do not all miss together.</summary>
    public static MemoryCacheEntryOptions Entry(TimeSpan lifetime, int size = 1)
    {
        var jitter = 0.9 + Random.Shared.NextDouble() * 0.2;
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(lifetime.TotalMilliseconds * jitter),
            Size = Math.Max(1, size)
        };
    }
}
