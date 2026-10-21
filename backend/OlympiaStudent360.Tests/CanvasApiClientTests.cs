using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using OlympiaStudent360.CanvasEtl;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class CanvasApiClientTests
{
    [Fact]
    public async Task ConcurrencyIsBoundedAcrossCoursesAndPagination()
    {
        var active = 0;
        var peak = 0;
        var requests = 0;
        using var handler = new Handler(async (request, token) =>
        {
            var running = Interlocked.Increment(ref active);
            Interlocked.Increment(ref requests);
            int previous;
            do { previous = Volatile.Read(ref peak); if (previous >= running) break; }
            while (Interlocked.CompareExchange(ref peak, running, previous) != previous);
            try
            {
                await Task.Delay(10, token);
                var response = Json();
                if (!request.RequestUri!.Query.Contains("page=2"))
                    response.Headers.Add("Link", $"<https://canvas.test{request.RequestUri.AbsolutePath}?page=2>; rel=\"next\"");
                return response;
            }
            finally { Interlocked.Decrement(ref active); }
        });
        using var http = new HttpClient(handler);
        var client = Client(http, 3);
        await Task.WhenAll(Enumerable.Range(1, 12).Select(id => client.GetAssignments(id, default)));
        Assert.Equal(24, requests);
        Assert.InRange(peak, 2, 3);
        Assert.Equal(24, client.RequestCount);
    }

    [Fact]
    public async Task ThrottlingPausesOtherRequestsAndCancellationDoesNotLeakSlots()
    {
        var times = new List<TimeSpan>();
        var elapsed = Stopwatch.StartNew();
        using var handler = new Handler((_, _) =>
        {
            times.Add(elapsed.Elapsed);
            var response = Json();
            if (times.Count == 1)
            {
                response.StatusCode = HttpStatusCode.TooManyRequests;
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMilliseconds(250));
            }
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var client = Client(http, 1);
        var first = client.GetAssignments(1, default);
        using var cancel = new CancellationTokenSource();
        var cancelled = client.GetAssignments(2, cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var next = client.GetAssignments(3, default);
        await Task.WhenAll(first, next).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(3, times.Count);
        Assert.True(times[1] - times[0] >= TimeSpan.FromMilliseconds(200));
        Assert.Equal(1, client.RetryCount);
    }

    private static CanvasApiClient Client(HttpClient http, int concurrency) => new(http, new CanvasEtlOptions
    {
        BaseUri = new Uri("https://canvas.test"), AccessToken = "test-token", AccountId = "1",
        MaxConcurrentRequests = concurrency
    });

    private static HttpResponseMessage Json() => new(HttpStatusCode.OK) { Content = new StringContent("[]") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
}
