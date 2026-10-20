using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class DatabaseResilienceTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseResilientMySql("Server=localhost;Database=unused;User=test;Password=test").Options);

    [Fact]
    public async Task HealthProbeWithAuthenticatedCookieDoesNotLoadDatabaseAccessRules()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], "cookie"));
        var nextCalled = false;
        await new UserAccessMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context, null!);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task RetriesWrappedTransientFailure()
    {
        await using var db = CreateContext();
        var attempts = 0;
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
        {
            if (++attempts == 1) throw new DbUpdateException("write failed", new TimeoutException());
            return Task.FromResult(42);
        });
        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task DoesNotRetryPermanentFailure()
    {
        await using var db = CreateContext();
        var attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            {
                attempts++;
                return Task.FromException(new InvalidOperationException("invalid configuration"));
            }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task PersistentTransientFailureHasBoundedRetries()
    {
        await using var db = CreateContext();
        var attempts = 0;
        await Assert.ThrowsAsync<RetryLimitExceededException>(() =>
            ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(() =>
            {
                attempts++;
                return Task.FromException(new TimeoutException());
            }));
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task CancellationDoesNotRetry()
    {
        await using var db = CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(token =>
            {
                attempts++;
                return Task.CompletedTask;
            }, cancellation.Token));
        Assert.Equal(0, attempts);
    }
}
