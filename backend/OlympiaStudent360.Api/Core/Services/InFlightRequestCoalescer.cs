using System.Collections.Concurrent;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Shares one in-progress computation between callers with the same key. Callers may stop
/// waiting independently; the shared computation remains bounded by its configured duration.
/// </summary>
public sealed class InFlightRequestCoalescer
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _requests = new();

    public async Task<T> RunAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan maxDuration,
        CancellationToken callerCancellationToken)
    {
        Lazy<Task<object?>>? candidate = null;
        candidate = new Lazy<Task<object?>>(async () =>
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    callerCancellationToken);
                timeout.CancelAfter(maxDuration);
                return await factory(timeout.Token);
            }
            finally
            {
                _requests.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(key, candidate!));
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        var request = _requests.GetOrAdd(key, candidate);
        // The owner awaits the factory itself so its scoped dependencies cannot be disposed
        // while work is still running. Joiners may stop waiting independently.
        var result = ReferenceEquals(request, candidate)
            ? await request.Value
            : await request.Value.WaitAsync(callerCancellationToken);
        return (T)result!;
    }
}
