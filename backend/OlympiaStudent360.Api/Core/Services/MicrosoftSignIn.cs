using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class MicrosoftSignInSettings
{
    public bool Enabled { get; private init; }
    public Guid TenantId { get; private init; }
    public Guid ClientId { get; private init; }
    public string PublicOrigin { get; private init; } = "";
    private string ClientSecret { get; init; } = "";

    public static MicrosoftSignInSettings Load(IConfiguration configuration)
    {
        var section = configuration.GetSection("MicrosoftSignIn");
        if (!section.GetValue<bool>("Enabled")) return new();

        if (!Guid.TryParse(section["TenantId"], out var tenantId) || tenantId == Guid.Empty ||
            !Guid.TryParse(section["ClientId"], out var clientId) || clientId == Guid.Empty ||
            string.IsNullOrWhiteSpace(section["ClientSecret"]) ||
            !Uri.TryCreate(section["PublicOrigin"], UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/" ||
            origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            throw new InvalidOperationException(
                "MicrosoftSignIn requires TenantId, ClientId, ClientSecret, and an HTTPS PublicOrigin.");
        }

        return new()
        {
            Enabled = true,
            TenantId = tenantId,
            ClientId = clientId,
            ClientSecret = section["ClientSecret"]!,
            PublicOrigin = origin.GetLeftPart(UriPartial.Authority)
        };
    }

    public void Configure(OpenIdConnectOptions options)
    {
        options.Authority = $"https://login.microsoftonline.com/{TenantId:D}/v2.0";
        options.ClientId = ClientId.ToString("D");
        options.ClientSecret = ClientSecret;
        options.CallbackPath = MicrosoftSignIn.CallbackPath;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query;
        options.UsePkce = true;
        options.MapInboundClaims = false;
        options.SaveTokens = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(10);
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                // CloudFront and App Runner can change the scheme and host seen by Kestrel.
                context.ProtocolMessage.RedirectUri = PublicOrigin + MicrosoftSignIn.CallbackPath;
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            },
            OnTicketReceived = async context =>
            {
                var services = context.HttpContext.RequestServices;
                var users = services.GetRequiredService<UserManager<ApplicationUser>>();
                var identity = MicrosoftSignIn.ProviderKey(TenantId, context.Principal);
                var user = identity is null ? null : await users.FindByLoginAsync(MicrosoftSignIn.Scheme, identity);
                if (user is null || await users.IsLockedOutAsync(user))
                {
                    context.HandleResponse();
                    context.Response.Redirect(PublicOrigin + "/login?microsoft=unlinked");
                    return;
                }

                user.LastLoginAt = DateTime.UtcNow;
                await users.UpdateAsync(user);
                services.GetRequiredService<ApplicationSession>().Issue(context.HttpContext, user, [], identity);
                context.HandleResponse();
                context.Response.Redirect(PublicOrigin + "/login?microsoft=success");
            },
            OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(PublicOrigin + "/login?microsoft=failed");
                return Task.CompletedTask;
            }
        };
    }
}

public static class MicrosoftSignIn
{
    public const string Scheme = "MicrosoftEntra";
    public const string StartPath = "/api/auth/microsoft/start";
    public const string CallbackPath = "/api/auth/microsoft/callback";
    public const string SessionIdentityClaim = "entra_identity";
    public const string SessionStampClaim = "entra_session_stamp";

    public static string ProviderKey(Guid tenantId, Guid objectId) => $"{tenantId:D}:{objectId:D}";

    internal static string? ProviderKey(Guid expectedTenant, ClaimsPrincipal? principal)
    {
        if (!Guid.TryParse(principal?.FindFirstValue("tid"), out var tenantId) ||
            tenantId != expectedTenant ||
            !Guid.TryParse(principal?.FindFirstValue("oid"), out var objectId) ||
            objectId == Guid.Empty)
        {
            return null;
        }
        return ProviderKey(tenantId, objectId);
    }

    public static async Task ValidateSessionAsync(Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext context)
    {
        var identity = context.Principal?.FindFirstValue(SessionIdentityClaim);
        if (identity is null) return;

        var services = context.HttpContext.RequestServices;
        var settings = services.GetRequiredService<MicrosoftSignInSettings>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = settings.Enabled && identity.StartsWith($"{settings.TenantId:D}:", StringComparison.Ordinal)
            ? await users.FindByLoginAsync(Scheme, identity)
            : null;
        var principal = context.Principal!;
        if (user is null || user.Id != principal.FindFirstValue(ClaimTypes.NameIdentifier) ||
            user.SecurityStamp != principal.FindFirstValue(SessionStampClaim) ||
            await users.IsLockedOutAsync(user))
        {
            context.Fail("Microsoft sign-in is no longer available for this account.");
            return;
        }

        var roles = await users.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            context.Fail("This Student 360 account has no assigned role.");
            return;
        }

        var claims = (ClaimsIdentity)principal.Identity!;
        foreach (var role in roles)
        {
            claims.AddClaim(new Claim(claims.RoleClaimType, role));
        }
    }
}
