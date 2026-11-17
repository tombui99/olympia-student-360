using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public class MicrosoftSignInTests
{
    private const string PublicOrigin = "https://s360.example.test";
    private const string Issuer = "https://login.microsoftonline.com/08d59488-d87f-4140-9e89-64ad0d78b443/v2.0";
    private const string SigningKey = "local-test-key-only-never-used-in-deployment";
    private static readonly Guid Tenant = Guid.Parse("08d59488-d87f-4140-9e89-64ad0d78b443");
    private static readonly Guid Client = Guid.Parse("181f1c26-3cb7-43f5-bc0f-b2262687f3f7");
    private static readonly Guid LinkedObject = Guid.Parse("56a3903f-7b98-42ad-a147-03ab62b7ef02");

    [Fact]
    public async Task AdminLinkUsesSavedEmailAndKeepsTheDirectoryObjectId()
    {
        var settings = DirectorySettings();
        using var handler = new DirectoryEndpoint([
            new { id = LinkedObject, mail = "PILOT@example.test", userPrincipalName = "different@example.test", userType = "Member" }
        ]);
        using var http = new HttpClient(handler);
        using var users = new PilotUsers { Linked = false };
        var controller = new AdminMicrosoftSignInController(users, settings, new MicrosoftDirectory(http, settings));

        Assert.IsType<ConflictObjectResult>(await controller.Link("student360-pilot", new("changed@example.test"), default));
        Assert.Equal(0, handler.LookupCount);
        Assert.IsType<NoContentResult>(await controller.Link("student360-pilot", new("pilot@example.test"), default));
        Assert.Equal(1, handler.LookupCount);
        Assert.Equal("mail eq 'pilot@example.test' or userPrincipalName eq 'pilot@example.test'", handler.Filter);
        Assert.Equal(MicrosoftSignIn.ProviderKey(Tenant, LinkedObject), users.AddedLogin?.ProviderKey);
        Assert.IsType<NoContentResult>(await controller.Link("student360-pilot", new("pilot@example.test"), default));
        Assert.Equal(1, handler.LookupCount);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("mismatch")]
    [InlineData("duplicate")]
    [InlineData("next-page")]
    [InlineData("missing-consent")]
    public async Task DirectoryRejectsUnverifiedOrAmbiguousEmailMatches(string scenario)
    {
        const string email = "o'brien@example.test";
        var settings = DirectorySettings();
        var account = new
        {
            id = LinkedObject, mail = scenario == "mismatch" ? "other@example.test" : email,
            userType = scenario == "guest" ? "Guest" : "Member"
        };
        object[] accounts = scenario == "duplicate" ? [account, account with { id = Guid.NewGuid() }] : [account];
        using var http = new HttpClient(new DirectoryEndpoint(accounts, scenario));
        var directory = new MicrosoftDirectory(http, settings);
        if (scenario is "guest" or "mismatch")
        {
            Assert.Null(await directory.FindMemberAsync(email, default));
        }
        else
        {
            var error = await Assert.ThrowsAsync<MicrosoftDirectoryException>(() => directory.FindMemberAsync(email, default));
            Assert.Equal(scenario == "missing-consent" ? "microsoft_directory_permission" : "microsoft_ambiguous", error.Code);
        }
    }

    internal static MicrosoftSignInSettings DirectorySettings() => MicrosoftSignInSettings.Load(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MicrosoftSignIn:Enabled"] = "true",
            ["MicrosoftSignIn:TenantId"] = Tenant.ToString(),
            ["MicrosoftSignIn:ClientId"] = Client.ToString(),
            ["MicrosoftSignIn:ClientSecret"] = "test-credential",
            ["MicrosoftSignIn:PublicOrigin"] = PublicOrigin
        }).Build());

    [Theory]
    [InlineData("linked")]
    [InlineData("same-email-without-link")]
    [InlineData("wrong-tenant")]
    public async Task CallbackUsesValidatedTenantAndObjectIdAndIssuesOnlyLocalSession(string scenario)
    {
        using var rsa = RSA.Create(2048);
        var issuerKey = new RsaSecurityKey(rsa) { KeyId = "test-issuer" };
        var localKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var backchannel = new TokenEndpoint(issuerKey,
            scenario == "wrong-tenant" ? Guid.NewGuid() : Tenant,
            scenario == "same-email-without-link" ? Guid.NewGuid() : LinkedObject);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MicrosoftSignIn:Enabled"] = "true",
            ["MicrosoftSignIn:TenantId"] = Tenant.ToString(),
            ["MicrosoftSignIn:ClientId"] = Client.ToString(),
            ["MicrosoftSignIn:ClientSecret"] = "test-credential",
            ["MicrosoftSignIn:PublicOrigin"] = PublicOrigin,
            ["Jwt:SigningKey"] = SigningKey,
            ["Jwt:Issuer"] = "student360",
            ["Jwt:Audience"] = "student360"
        });
        var settings = MicrosoftSignInSettings.Load(builder.Configuration);
        using var users = new PilotUsers();
        builder.Services.AddSingleton<UserManager<ApplicationUser>>(users);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<ApplicationSession>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddOpenIdConnect(MicrosoftSignIn.Scheme, options =>
            {
                settings.Configure(options);
                var metadata = new OpenIdConnectConfiguration
                {
                    Issuer = Issuer,
                    AuthorizationEndpoint = Issuer + "/authorize",
                    TokenEndpoint = Issuer + "/token"
                };
                metadata.SigningKeys.Add(issuerKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                options.BackchannelHttpHandler = backchannel;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidIssuer = "student360",
                    ValidAudience = "student360",
                    IssuerSigningKey = localKey
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies[ApplicationSession.CookieName];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = MicrosoftSignIn.ValidateSessionAsync
                };
            });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet(MicrosoftSignIn.StartPath, () =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = "/login" }, [MicrosoftSignIn.Scheme]));
        app.MapGet("/me", (ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier))
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));
        await app.StartAsync();
        using var browser = app.GetTestClient();
        browser.BaseAddress = new Uri("http://app-runner.internal");

        using var start = await browser.GetAsync(MicrosoftSignIn.StartPath);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        Assert.Equal(PublicOrigin + MicrosoftSignIn.CallbackPath, query["redirect_uri"].ToString());
        Assert.Equal("form_post", query["response_mode"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        backchannel.Nonce = query["nonce"].ToString();
        var signInCookies = start.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Equal(2, signInCookies.Length);
        Assert.All(signInCookies, cookie =>
        {
            Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        });
        var cookies = string.Join("; ", signInCookies.Select(FirstCookie));
        using var callbackRequest = new HttpRequestMessage(HttpMethod.Post, MicrosoftSignIn.CallbackPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "test-code-" + new string('a', 3000),
                ["state"] = query["state"].ToString()
            })
        };
        callbackRequest.Headers.Add("Origin", "https://login.microsoftonline.com");
        callbackRequest.Headers.Add("Cookie", cookies);
        using var callback = await browser.SendAsync(callbackRequest);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var issuedCookie = callback.Headers.GetValues("Set-Cookie")
            .SingleOrDefault(value => value.StartsWith(ApplicationSession.CookieName + "="));

        if (scenario != "linked")
        {
            Assert.Equal(PublicOrigin + "/login?microsoft=unlinked", callback.Headers.Location!.AbsoluteUri);
            Assert.Null(issuedCookie);
            return;
        }

        Assert.Equal(PublicOrigin + "/login?microsoft=success", callback.Headers.Location!.AbsoluteUri);
        Assert.NotNull(issuedCookie);
        Assert.Contains("secure", issuedCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", issuedCookie, StringComparison.OrdinalIgnoreCase);
        var session = FirstCookie(issuedCookie);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session[(session.IndexOf('=') + 1)..]);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
        browser.DefaultRequestHeaders.Add("Cookie", session);
        Assert.Equal("student360-pilot", await browser.GetStringAsync("/me"));

        users.Linked = false;
        using var revoked = await browser.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    private static string FirstCookie(string value) => value.Split(';', 2)[0];

    private sealed class TokenEndpoint(SecurityKey key, Guid tenant, Guid objectId) : HttpMessageHandler
    {
        public string Nonce { get; set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("code_verifier=", body);
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(Issuer, Client.ToString(), [
                new("sub", "microsoft-subject"), new("tid", tenant.ToString()), new("oid", objectId.ToString()),
                new("email", "pilot@example.test"), new("nonce", Nonce),
                new("iat", EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)
            ], now.AddMinutes(-1), now.AddMinutes(5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id_token = new JwtSecurityTokenHandler().WriteToken(token),
                    access_token = "test-access-token",
                    token_type = "Bearer"
                })
            };
        }
    }

    internal sealed class DirectoryEndpoint(object[] accounts, string scenario = "", object? byId = null) : HttpMessageHandler
    {
        public string? Filter { get; private set; }
        public int LookupCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal($"https://login.microsoftonline.com/{Tenant:D}/oauth2/v2.0/token", request.RequestUri!.AbsoluteUri);
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("client_credentials", form["grant_type"].ToString());
                Assert.Equal("https://graph.microsoft.com/.default", form["scope"].ToString());
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "test-graph-token" }) };
            }
            Assert.Equal("Bearer test-graph-token", request.Headers.Authorization?.ToString());
            if (request.RequestUri!.AbsolutePath.StartsWith("/v1.0/users/", StringComparison.Ordinal))
            {
                Assert.Equal("https://graph.microsoft.com", request.RequestUri.GetLeftPart(UriPartial.Authority));
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(byId) };
            }
            LookupCount++;
            Assert.Equal("https://graph.microsoft.com/v1.0/users", request.RequestUri!.GetLeftPart(UriPartial.Path));
            Filter = QueryHelpers.ParseQuery(request.RequestUri.Query)["$filter"].ToString();
            if (scenario.Length > 0) Assert.Contains("'o''brien@example.test'", Filter);
            if (scenario == "missing-consent") return new(HttpStatusCode.Forbidden);
            var result = new Dictionary<string, object> { ["value"] = accounts };
            if (scenario == "next-page") result["@odata.nextLink"] = "next";
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
    }

    private sealed class PilotUsers() : UserManager<ApplicationUser>(
        new UserStore<ApplicationUser>(new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options)),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        private readonly ApplicationUser user = new()
        {
            Id = "student360-pilot", UserName = "pilot", Email = "pilot@example.test", SecurityStamp = "pilot-stamp"
        };
        public bool Linked { get; set; } = true;
        public UserLoginInfo? AddedLogin { get; private set; }

        public override Task<ApplicationUser?> FindByIdAsync(string userId) =>
            Task.FromResult(user.Id == userId ? user : null);

        public override Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user) =>
            Task.FromResult<IList<UserLoginInfo>>(AddedLogin is null ? [] : [AddedLogin]);

        public override Task<IdentityResult> AddLoginAsync(ApplicationUser user, UserLoginInfo login)
        {
            AddedLogin = login;
            return Task.FromResult(IdentityResult.Success);
        }

        public override Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey) =>
            Task.FromResult(Linked && loginProvider == MicrosoftSignIn.Scheme &&
                providerKey == MicrosoftSignIn.ProviderKey(Tenant, LinkedObject) ? user : null);

        public override Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);
        public override Task<IdentityResult> UpdateAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Success);
        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) => Task.FromResult<IList<string>>(["Teacher"]);
    }
}

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task MicrosoftFirstLoginMatchesOneExistingAccountAndRespectsRevocation()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<AppDbContext>(_ => test.Open());
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const string email = "staff@example.test";
        var account = new ApplicationUser { UserName = "staff", Email = email, EmailConfirmed = true, FullName = "Staff" };
        Assert.True((await users.CreateAsync(account)).Succeeded);
        var settings = MicrosoftSignInTests.DirectorySettings();
        var id = Guid.NewGuid();
        var identity = MicrosoftSignIn.ProviderKey(settings.TenantId, id);
        var graphUser = new { id, mail = email, userType = "Member" };
        using var graph = new HttpClient(new MicrosoftSignInTests.DirectoryEndpoint([graphUser], byId: graphUser));
        var directory = new MicrosoftDirectory(graph, settings);

        Assert.Null(await MicrosoftSignIn.LinkFirstLoginAsync(users, directory, id, identity, default));
        Assert.True((await roles.CreateAsync(new IdentityRole("Teacher"))).Succeeded);
        Assert.True((await users.AddToRoleAsync(account, "Teacher")).Succeeded);
        var duplicate = new ApplicationUser { UserName = "another-staff", Email = email, EmailConfirmed = true, FullName = "Another" };
        Assert.True((await users.CreateAsync(duplicate)).Succeeded);
        Assert.Null(await MicrosoftSignIn.LinkFirstLoginAsync(users, directory, id, identity, default));
        Assert.True((await users.DeleteAsync(duplicate)).Succeeded);

        var otherGraphUser = graphUser with { id = Guid.NewGuid() };
        using var wrongGraph = new HttpClient(new MicrosoftSignInTests.DirectoryEndpoint([otherGraphUser], byId: graphUser));
        Assert.Null(await MicrosoftSignIn.LinkFirstLoginAsync(users, new MicrosoftDirectory(wrongGraph, settings), id, identity, default));

        var linked = await MicrosoftSignIn.LinkFirstLoginAsync(users, directory, id, identity, default);
        Assert.Equal(account.Id, linked?.Id);
        Assert.Equal(account.Id, (await users.FindByLoginAsync(MicrosoftSignIn.Scheme, identity))?.Id);
        Assert.Equal(new[] { "Teacher" }, await users.GetRolesAsync(linked!));

        var admin = new AdminMicrosoftSignInController(users, settings, directory);
        Assert.IsType<NoContentResult>(await admin.Unlink(account.Id));
        db.ChangeTracker.Clear();
        Assert.True((await users.FindByIdAsync(account.Id))?.MicrosoftSignInBlocked);
        Assert.Null(await users.FindByLoginAsync(MicrosoftSignIn.Scheme, identity));
        Assert.Null(await MicrosoftSignIn.LinkFirstLoginAsync(users, directory, id, identity, default));

        Assert.IsType<NoContentResult>(await admin.Link(account.Id, new(email), default));
        db.ChangeTracker.Clear();
        var restored = await users.FindByLoginAsync(MicrosoftSignIn.Scheme, identity);
        Assert.Equal(account.Id, restored?.Id);
        Assert.False(restored?.MicrosoftSignInBlocked);
    }
}
