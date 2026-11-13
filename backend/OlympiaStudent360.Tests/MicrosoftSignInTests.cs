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
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
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
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        backchannel.Nonce = query["nonce"].ToString();
        var cookies = string.Join("; ", start.Headers.GetValues("Set-Cookie").Select(FirstCookie));
        var callbackPath = QueryHelpers.AddQueryString(MicrosoftSignIn.CallbackPath,
            new Dictionary<string, string?> { ["code"] = "test-code", ["state"] = query["state"] });
        using var callbackRequest = new HttpRequestMessage(HttpMethod.Get, callbackPath);
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

        public override Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey) =>
            Task.FromResult(Linked && loginProvider == MicrosoftSignIn.Scheme &&
                providerKey == MicrosoftSignIn.ProviderKey(Tenant, LinkedObject) ? user : null);

        public override Task<bool> IsLockedOutAsync(ApplicationUser user) => Task.FromResult(false);
        public override Task<IdentityResult> UpdateAsync(ApplicationUser user) => Task.FromResult(IdentityResult.Success);
        public override Task<IList<string>> GetRolesAsync(ApplicationUser user) => Task.FromResult<IList<string>>(["Teacher"]);
    }
}
