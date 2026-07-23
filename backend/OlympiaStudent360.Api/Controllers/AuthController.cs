using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
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

        return Ok(new AuthResponseDto
        {
            Token = serializedToken,
            Role = roles.FirstOrDefault() ?? "Teacher",
            Username = user.UserName!,
            FullName = user.FullName,
            UserId = user.Id
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
        return Ok(new CurrentUserDto
        {
            Role = roles.FirstOrDefault() ?? "Teacher",
            Username = user.UserName!,
            FullName = user.FullName,
            UserId = user.Id
        });
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
}
