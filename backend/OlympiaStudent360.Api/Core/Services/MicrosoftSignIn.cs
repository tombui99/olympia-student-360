using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class MicrosoftSignInSettings
{
    public bool Enabled { get; private init; }
    public bool AutoLinkByEmail { get; private init; }
    public Guid TenantId { get; private init; }
    public Guid ClientId { get; private init; }
    public string PublicOrigin { get; private init; } = "";
    internal string ClientSecret { get; private init; } = "";

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
            AutoLinkByEmail = section.GetValue<bool>("AutoLinkByEmail"),
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
        // Entra authorization responses can exceed the CloudFront WAF query string limit.
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;
        options.MapInboundClaims = false;
        options.SaveTokens = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        // Microsoft posts back from another site. Keep the handler's secure, SameSite=None cookies.
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
                var objectId = MicrosoftSignIn.ObjectId(TenantId, context.Principal);
                var identity = objectId is { } id ? MicrosoftSignIn.ProviderKey(TenantId, id) : null;
                var user = identity is null ? null : await users.FindByLoginAsync(MicrosoftSignIn.Scheme, identity);
                if (user is null && AutoLinkByEmail && objectId is { } unlinkedId)
                {
                    try
                    {
                        user = await MicrosoftSignIn.LinkFirstLoginAsync(users,
                            services.GetRequiredService<MicrosoftDirectory>(), unlinkedId, identity!,
                            context.HttpContext.RequestAborted);
                    }
                    catch (MicrosoftDirectoryException error)
                    {
                        services.GetRequiredService<ILogger<MicrosoftSignInSettings>>()
                            .LogWarning("Microsoft directory lookup failed with {Code}.", error.Code);
                        context.HandleResponse();
                        context.Response.Redirect(PublicOrigin + "/login?microsoft=failed");
                        return;
                    }
                }
                if (user is null || user.MicrosoftSignInBlocked || await users.IsLockedOutAsync(user))
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

    internal static Guid? ObjectId(Guid expectedTenant, ClaimsPrincipal? principal)
    {
        if (!Guid.TryParse(principal?.FindFirstValue("tid"), out var tenantId) ||
            tenantId != expectedTenant ||
            !Guid.TryParse(principal?.FindFirstValue("oid"), out var objectId) ||
            objectId == Guid.Empty)
        {
            return null;
        }
        return objectId;
    }

    internal static async Task<ApplicationUser?> LinkFirstLoginAsync(UserManager<ApplicationUser> users,
        MicrosoftDirectory directory, Guid objectId, string identity, CancellationToken ct)
    {
        var addresses = await directory.GetMemberAddressesAsync(objectId, ct);
        var account = await FindUniqueAccountAsync(users, addresses, ct);
        if (account is null || !account.EmailConfirmed || account.MicrosoftSignInBlocked ||
            await users.IsLockedOutAsync(account))
            return null;
        if ((await users.GetLoginsAsync(account)).Any(login => login.LoginProvider == Scheme) ||
            (await users.GetRolesAsync(account)).Count == 0)
            return null;

        try
        {
            // Check both directions so a duplicate address or a different directory account
            // never establishes a link, even when the ID token itself is valid.
            if (await directory.FindMemberAsync(account.Email!.Trim(), ct) != objectId) return null;
            var result = await users.AddLoginAsync(account, new UserLoginInfo(Scheme, identity, "Microsoft"));
            return result.Succeeded ? account : null;
        }
        catch (MicrosoftDirectoryException error) when (error.Code == "microsoft_ambiguous")
        {
            return null;
        }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
        {
            // Another request established a link. A fresh sign-in can read the committed row.
            return null;
        }
    }

    internal static async Task<ApplicationUser?> FindUniqueAccountAsync(
        UserManager<ApplicationUser> users, string[] addresses, CancellationToken ct)
    {
        if (addresses.Length == 0) return null;
        var firstEmail = users.NormalizeEmail(addresses[0]);
        var secondEmail = addresses.Length > 1 ? users.NormalizeEmail(addresses[1]) : firstEmail;
        var accounts = await users.Users.Where(user => user.NormalizedEmail != null &&
                (user.NormalizedEmail == firstEmail || user.NormalizedEmail == secondEmail))
            .Take(2).ToListAsync(ct);
        return accounts.Count == 1 &&
            addresses.Contains(accounts[0].Email?.Trim(), StringComparer.OrdinalIgnoreCase) ? accounts[0] : null;
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
        if (user is null || user.MicrosoftSignInBlocked || user.Id != principal.FindFirstValue(ClaimTypes.NameIdentifier) ||
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
