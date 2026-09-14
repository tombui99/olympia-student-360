using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/users")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public UsersController(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("directory")]
    public async Task<ActionResult<IEnumerable<UserDirectoryDto>>> Directory()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var users = await _userManager.Users
            .Where(user => user.Id != currentUserId)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .ToListAsync();

        var assignedRoles = await (
            from userRole in _context.UserRoles
            join role in _context.Roles on userRole.RoleId equals role.Id
            select new { userRole.UserId, role.Name }
        ).ToListAsync();
        var rolesByUserId = assignedRoles
            .GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        return Ok(users.Select(user => new UserDirectoryDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FullName = user.FullName,
            Role = rolesByUserId.GetValueOrDefault(user.Id)
        }));
    }
}
