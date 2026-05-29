using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/admin/users")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private static readonly string[] ManagedRoles = ["Admin", "Principal", "Teacher"];
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var users = await _userManager.Users
            .Where(user => user.Id != currentUserId)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .ToListAsync();

        var userDtos = new List<AdminUserDto>();
        foreach (var user in users)
        {
            userDtos.Add(await ToDto(user));
        }

        return Ok(userDtos);
    }

    [HttpGet("roles")]
    public ActionResult<List<string>> GetRoles()
    {
        return Ok(ManagedRoles.ToList());
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> CreateUser([FromBody] CreateAdminUserDto model)
    {
        if (!await IsValidRole(model.Role))
        {
            return BadRequest(new { Message = $"Role '{model.Role}' does not exist." });
        }

        var existingUser = await _userManager.FindByNameAsync(model.Username);
        if (existingUser != null)
        {
            return Conflict(new { Message = "User already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email)
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(error => error.Description) });
        }

        await _userManager.AddToRoleAsync(user, model.Role);
        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, await ToDto(user));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(string id, [FromBody] UpdateAdminUserDto model)
    {
        if (!await IsValidRole(model.Role))
        {
            return BadRequest(new { Message = $"Role '{model.Role}' does not exist." });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains("Admin") && model.Role != "Admin" && await IsLastAdmin(user.Id))
        {
            return BadRequest(new { Message = "Cannot remove the last admin account." });
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = user.UserName;
        user.EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new { Errors = updateResult.Errors.Select(error => error.Description) });
        }

        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        await _userManager.AddToRoleAsync(user, model.Role);

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
            if (!passwordResult.Succeeded)
            {
                return BadRequest(new { Errors = passwordResult.Errors.Select(error => error.Description) });
            }
        }

        return Ok(await ToDto(user));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == currentUserId)
        {
            return BadRequest(new { Message = "You cannot delete your own account." });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin") && await IsLastAdmin(user.Id))
        {
            return BadRequest(new { Message = "Cannot delete the last admin account." });
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { Errors = result.Errors.Select(error => error.Description) });
        }

        return NoContent();
    }

    private async Task<bool> IsValidRole(string role)
    {
        return ManagedRoles.Contains(role) && await _roleManager.RoleExistsAsync(role);
    }

    private async Task<bool> IsLastAdmin(string userId)
    {
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        return admins.Count == 1 && admins[0].Id == userId;
    }

    private async Task<AdminUserDto> ToDto(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new AdminUserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FullName = user.FullName,
            Role = roles.FirstOrDefault() ?? "Teacher",
            CreatedAt = AsUtc(user.CreatedAt),
            LastLoginAt = user.LastLoginAt.HasValue ? AsUtc(user.LastLoginAt.Value) : null
        };
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
