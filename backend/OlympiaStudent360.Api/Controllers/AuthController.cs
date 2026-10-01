using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly UserAccessService _userAccess;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        UserAccessService userAccess)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _userAccess = userAccess;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto model)
    {
        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized(new { Message = "Invalid username or password." });
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var token = CreateToken(user, roles);
        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("olympia_student_360_auth", serializedToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });

        var role = roles.FirstOrDefault() ?? "Teacher";
        return Ok(new AuthResponseDto
        {
            Token = serializedToken,
            Role = role,
            Username = user.UserName!,
            FullName = user.FullName,
            UserId = user.Id,
            Access = ToAccessDto(await _userAccess.LoadAsync(user.Id, role))
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        var existingUser = await _userManager.FindByNameAsync(model.Username);
        if (existingUser != null)
        {
            return Conflict(new { Message = "User already exists." });
        }

        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            return BadRequest(new { Message = $"Role '{model.Role}' does not exist." });
        }

        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Username.Contains('@') ? model.Username : null,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        return Ok(new { Status = "Success", Message = "User created successfully." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Teacher";
        return Ok(new CurrentUserDto
        {
            Role = role,
            Username = user.UserName!,
            FullName = user.FullName,
            UserId = user.Id,
            Access = ToAccessDto(await _userAccess.LoadAsync(user.Id, role))
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        if (string.Equals(model.CurrentPassword, model.NewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                Errors = new[] { "The new password must be different from the current password." }
            });
        }

        var result = await _userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(error => error.Description) });
        }

        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("olympia_student_360_auth", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });

        return NoContent();
    }

    private JwtSecurityToken CreateToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
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

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SigningKey"]!));
        return new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
    }

    private static UserAccessCapabilitiesDto ToAccessDto(UserAccessSnapshot access) => new()
    {
        IsTeacher = access.IsTeacher,
        HasAll = access.HasAll,
        RestrictsClasses = access.RestrictsClasses,
        RestrictsStudents = access.RestrictsStudents,
        Scopes = access.Scopes.OrderBy(item => item).ToList(),
        Homerooms = access.Homerooms.ToList(),
        SchoolLevels = access.SchoolLevels.ToList(),
        CanAccessHealth = access.CanAccessHealth,
        CanAccessBus = access.CanAccessBus,
        CanAccessAcademic = access.CanAccessAcademic,
        CanAccessWorkspace = access.CanAccessWorkspace,
        CanAccessAtRisk = access.CanAccessAtRisk,
        CanAccessCommendations = access.CanAccessCommendations,
        CanAccessSupport = access.CanAccessSupport
    };
}
