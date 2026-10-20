using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class DatabaseResilienceTests
{
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Production", "true", true)]
    [InlineData("Development", null, true)]
    [InlineData("Development", "false", false)]
    public async Task ApiReportBackfillDefaultsToCliInProductionAndAllowsExplicitOverride(
        string environmentName, string? configured, bool shouldRun)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Canvas:EnableApiReportBackfill"] = configured }).Build();
        var scopeFactory = new ObservedScopeFactory();
        using var worker = new CanvasReportBackfillWorker(scopeFactory, configuration,
            new TestEnvironment { EnvironmentName = environmentName },
            NullLogger<CanvasReportBackfillWorker>.Instance);
        await worker.StartAsync(default);
        if (shouldRun)
            await scopeFactory.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        else
        {
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(scopeFactory.Requested.Task.IsCompleted);
        }
        await worker.StopAsync(default);
    }

    private sealed class ObservedScopeFactory : IServiceScopeFactory
    {
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IServiceScope CreateScope()
        {
            Requested.TrySetResult();
            throw new InvalidOperationException("Observed background scope request without opening a database.");
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

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
