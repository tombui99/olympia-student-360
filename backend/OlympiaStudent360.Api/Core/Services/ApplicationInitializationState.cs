namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Coordinates database initialization with HTTP requests and background workers.
/// The web server can become healthy before a long-running migration completes.
/// </summary>
public sealed class ApplicationInitializationState
{
    private readonly TaskCompletionSource ready = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int status;

    public bool IsReady => Volatile.Read(ref status) == 1;

    public string Status => Volatile.Read(ref status) switch
    {
        1 => "ready",
        2 => "failed",
        _ => "initializing"
    };

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) =>
        ready.Task.WaitAsync(cancellationToken);

    public void MarkReady()
    {
        Volatile.Write(ref status, 1);
        ready.TrySetResult();
    }

    public void MarkFailed() => Volatile.Write(ref status, 2);
}
