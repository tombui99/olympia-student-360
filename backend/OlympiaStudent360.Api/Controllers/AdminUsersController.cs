using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/admin/users")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private static readonly string[] ManagedRoles = ["Admin", "SchoolAdministration", "Teacher"];
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _context;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<AdminUserPageDto>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 50);
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _userManager.Users.AsNoTracking()
            .Where(user => user.Id != currentUserId);

        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            if (searchTerm.Length > 100) searchTerm = searchTerm[..100];
            query = query.Where(user =>
                user.FullName.Contains(searchTerm) ||
                (user.UserName != null && user.UserName.Contains(searchTerm)) ||
                (user.Email != null && user.Email.Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = ManagedRoles.FirstOrDefault(item =>
                string.Equals(item, role.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedRole == null)
                return BadRequest(new { Message = $"Role '{role}' is not supported." });

            var roleId = await _context.Roles.AsNoTracking()
                .Where(item => item.Name == normalizedRole)
                .Select(item => item.Id)
                .SingleOrDefaultAsync();
            if (roleId == null)
                return BadRequest(new { Message = $"Role '{normalizedRole}' does not exist." });

            query = query.Where(user => _context.UserRoles.Any(userRole =>
                userRole.UserId == user.Id && userRole.RoleId == roleId));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var pageQuery = query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return Ok(new AdminUserPageDto
        {
            Items = await ToDtos(pageQuery),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    [HttpGet("roles")]
    public ActionResult<List<string>> GetRoles()
    {
        return Ok(ManagedRoles.ToList());
    }

    [HttpGet("options")]
    public async Task<ActionResult<AdminUserOptionsDto>> GetOptions()
    {
        return Ok(new AdminUserOptionsDto
        {
            Roles = ManagedRoles.ToList(),
            AccessScopes = TeacherAccessScopes.Values.ToList(),
            SchoolLevels = TeacherSchoolLevels.Values.ToList(),
            Homerooms = await _context.StudentProfiles.AsNoTracking()
                .Where(item => item.Homeroom != null && item.Homeroom != "")
                .Select(item => item.Homeroom!)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync()
        });
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

        var accessError = await ValidateAccess(
            model.Role, model.AccessScopes, model.Homerooms, model.SchoolLevels);
        if (accessError != null) return BadRequest(new { Message = accessError });

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
        await SaveAccess(user.Id, model.Role, model.AccessScopes, model.Homerooms, model.SchoolLevels);

        return StatusCode(StatusCodes.Status201Created, await ToDto(user));
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

        var accessError = await ValidateAccess(
            model.Role, model.AccessScopes, model.Homerooms, model.SchoolLevels);
        if (accessError != null) return BadRequest(new { Message = accessError });

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
        await SaveAccess(user.Id, model.Role, model.AccessScopes, model.Homerooms, model.SchoolLevels);

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
        var role = roles.FirstOrDefault() ?? "Teacher";
        var scopes = role == "Teacher"
            ? await _context.TeacherAccessGrants.AsNoTracking().Where(item => item.UserId == user.Id)
                .Select(item => item.Scope).ToListAsync()
            : [];
        if (role == "Teacher" && scopes.Count == 0) scopes.Add(TeacherAccessScopes.All);
        return new AdminUserDto
        {
            Id = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FullName = user.FullName,
            Role = role,
            AccessScopes = scopes,
            Homerooms = role == "Teacher"
                ? await _context.TeacherClassAccesses.AsNoTracking().Where(item => item.UserId == user.Id)
                    .Select(item => item.Homeroom).OrderBy(item => item).ToListAsync()
                : [],
            SchoolLevels = role == "Teacher"
                ? await _context.TeacherSchoolLevelAccesses.AsNoTracking().Where(item => item.UserId == user.Id)
                    .Select(item => item.SchoolLevel).OrderBy(item => item).ToListAsync()
                : [],
            CreatedAt = AsUtc(user.CreatedAt),
            LastLoginAt = user.LastLoginAt.HasValue ? AsUtc(user.LastLoginAt.Value) : null
        };
    }

    private async Task<List<AdminUserDto>> ToDtos(IQueryable<ApplicationUser> pageQuery)
    {
        var users = await pageQuery.ToListAsync();
        if (users.Count == 0) return [];

        var roleRows = await (
            from user in pageQuery
            join userRole in _context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            select new { user.Id, Role = role.Name! }
        ).ToListAsync();
        var grantRows = await (
            from user in pageQuery
            join grant in _context.TeacherAccessGrants.AsNoTracking() on user.Id equals grant.UserId
            select new { user.Id, grant.Scope }
        ).ToListAsync();
        var classRows = await (
            from user in pageQuery
            join classAccess in _context.TeacherClassAccesses.AsNoTracking() on user.Id equals classAccess.UserId
            select new { user.Id, classAccess.Homeroom }
        ).ToListAsync();
        var schoolLevelRows = await (
            from user in pageQuery
            join schoolLevel in _context.TeacherSchoolLevelAccesses.AsNoTracking()
                on user.Id equals schoolLevel.UserId
            select new { user.Id, schoolLevel.SchoolLevel }
        ).ToListAsync();

        var rolesByUser = roleRows.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Role).FirstOrDefault());
        var grantsByUser = grantRows.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Scope).ToList());
        var classesByUser = classRows.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Homeroom)
                .OrderBy(item => item).ToList());
        var schoolLevelsByUser = schoolLevelRows.GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.Select(item => item.SchoolLevel)
                .OrderBy(item => item).ToList());

        return users.Select(user =>
        {
            var userRole = rolesByUser.GetValueOrDefault(user.Id) ?? "Teacher";
            var scopes = userRole == "Teacher"
                ? grantsByUser.GetValueOrDefault(user.Id) ?? []
                : [];
            if (userRole == "Teacher" && scopes.Count == 0) scopes = [TeacherAccessScopes.All];
            return new AdminUserDto
            {
                Id = user.Id,
                Username = user.UserName!,
                Email = user.Email,
                FullName = user.FullName,
                Role = userRole,
                AccessScopes = scopes,
                Homerooms = userRole == "Teacher"
                    ? classesByUser.GetValueOrDefault(user.Id) ?? []
                    : [],
                SchoolLevels = userRole == "Teacher"
                    ? schoolLevelsByUser.GetValueOrDefault(user.Id) ?? []
                    : [],
                CreatedAt = AsUtc(user.CreatedAt),
                LastLoginAt = user.LastLoginAt.HasValue ? AsUtc(user.LastLoginAt.Value) : null
            };
        }).ToList();
    }

    private async Task<string?> ValidateAccess(
        string role,
        List<string>? requestedScopes,
        List<string>? requestedHomerooms,
        List<string>? requestedSchoolLevels)
    {
        if (role != "Teacher") return null;
        var scopes = UserAccessService.NormalizeScopes(requestedScopes);
        if (scopes.Count == 0) scopes.Add(TeacherAccessScopes.All);
        var homerooms = UserAccessService.NormalizeHomerooms(requestedHomerooms);
        if (scopes.Contains(TeacherAccessScopes.Class))
        {
            if (homerooms.Count == 0) return "Select at least one class for class access.";
            var availableHomerooms = await _context.StudentProfiles.AsNoTracking()
                .Where(item => item.Homeroom != null && item.Homeroom != "")
                .Select(item => item.Homeroom!).Distinct().ToListAsync();
            if (homerooms.Except(availableHomerooms, StringComparer.OrdinalIgnoreCase).Any())
                return "One or more selected classes are invalid.";
        }
        if (scopes.Contains(TeacherAccessScopes.SchoolLevel) &&
            UserAccessService.NormalizeSchoolLevels(requestedSchoolLevels).Count == 0)
            return "Select at least one school level for school-level access.";
        return null;
    }

    private async Task SaveAccess(
        string userId,
        string role,
        List<string>? requestedScopes,
        List<string>? requestedHomerooms,
        List<string>? requestedSchoolLevels)
    {
        var oldGrants = await _context.TeacherAccessGrants.Where(item => item.UserId == userId).ToListAsync();
        var oldClasses = await _context.TeacherClassAccesses.Where(item => item.UserId == userId).ToListAsync();
        var oldSchoolLevels = await _context.TeacherSchoolLevelAccesses
            .Where(item => item.UserId == userId).ToListAsync();
        _context.TeacherAccessGrants.RemoveRange(oldGrants);
        _context.TeacherClassAccesses.RemoveRange(oldClasses);
        _context.TeacherSchoolLevelAccesses.RemoveRange(oldSchoolLevels);
        if (role == "Teacher")
        {
            var scopes = UserAccessService.NormalizeScopes(requestedScopes);
            if (scopes.Count == 0) scopes.Add(TeacherAccessScopes.All);
            _context.TeacherAccessGrants.AddRange(scopes.Select(scope =>
                new TeacherAccessGrant { UserId = userId, Scope = scope }));
            if (scopes.Contains(TeacherAccessScopes.Class))
            {
                _context.TeacherClassAccesses.AddRange(
                    UserAccessService.NormalizeHomerooms(requestedHomerooms).Select(homeroom =>
                        new TeacherClassAccess { UserId = userId, Homeroom = homeroom }));
            }
            if (scopes.Contains(TeacherAccessScopes.SchoolLevel))
            {
                _context.TeacherSchoolLevelAccesses.AddRange(
                    UserAccessService.NormalizeSchoolLevels(requestedSchoolLevels).Select(schoolLevel =>
                        new TeacherSchoolLevelAccess { UserId = userId, SchoolLevel = schoolLevel }));
            }
        }
        await _context.SaveChangesAsync();
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
