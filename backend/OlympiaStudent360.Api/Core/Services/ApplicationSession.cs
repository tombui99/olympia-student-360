using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class ApplicationSession(IConfiguration configuration)
{
    public const string CookieName = "olympia_student_360_auth";

    public string Issue(HttpContext context, ApplicationUser user, IEnumerable<string> roles,
        string? microsoftIdentity = null)
    {
        var settings = configuration.GetSection("Jwt");
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        DateTime? expires = null;
        if (microsoftIdentity is not null)
        {
            expires = DateTime.UtcNow.AddHours(1);
            claims.Add(new Claim(MicrosoftSignIn.SessionIdentityClaim, microsoftIdentity));
            claims.Add(new Claim(MicrosoftSignIn.SessionStampClaim, user.SecurityStamp!));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["SigningKey"]!));
        var token = new JwtSecurityToken(settings["Issuer"], settings["Audience"], claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        context.Response.Cookies.Append(CookieName, serialized, new CookieOptions
        {
            HttpOnly = true,
            Secure = microsoftIdentity is not null || context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires
        });
        return serialized;
    }
}
