using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/users")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("directory")]
    public async Task<ActionResult<IEnumerable<UserDirectoryDto>>> Directory()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var users = await _userManager.Users
            .Where(user => user.Id != currentUserId)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .Select(user => new UserDirectoryDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email,
                FullName = user.FullName
            })
            .ToListAsync();

        return Ok(users);
    }
}
