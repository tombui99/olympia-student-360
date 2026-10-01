using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/user-access")]
[ApiController]
[Authorize(Roles = "Admin,SchoolAdministration,Principal")]
public sealed class UserAccessController(
    AppDbContext context,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserAccessManagementDto>> Get()
    {
        var teachers = await userManager.GetUsersInRoleAsync("Teacher");
        // These tables contain only teacher access assignments and are small. Loading them once
        // also avoids the MySQL provider trying to translate an empty local teacher-id collection
        // when the school has not created any Teacher accounts yet.
        var grants = await context.TeacherAccessGrants.AsNoTracking()
            .ToListAsync();
        var classes = await context.TeacherClassAccesses.AsNoTracking()
            .ToListAsync();
        var schoolLevels = await context.TeacherSchoolLevelAccesses.AsNoTracking()
            .ToListAsync();

        return Ok(new UserAccessManagementDto
        {
            AvailableScopes = TeacherAccessScopes.Values.ToList(),
            AvailableSchoolLevels = TeacherSchoolLevels.Values.ToList(),
            AvailableHomerooms = await context.StudentProfiles.AsNoTracking()
                .Where(item => item.Homeroom != null && item.Homeroom != "")
                .Select(item => item.Homeroom!)
                .Distinct().OrderBy(item => item).ToListAsync(),
            Users = teachers.OrderBy(item => item.FullName).ThenBy(item => item.UserName)
                .Select(user => new TeacherAccessUserDto
                {
                    UserId = user.Id,
                    Username = user.UserName!,
                    Email = user.Email,
                    FullName = user.FullName,
                    Scopes = grants.Where(item => item.UserId == user.Id).Select(item => item.Scope).DefaultIfEmpty(TeacherAccessScopes.All).ToList(),
                    Homerooms = classes.Where(item => item.UserId == user.Id).Select(item => item.Homeroom).OrderBy(item => item).ToList(),
                    SchoolLevels = schoolLevels.Where(item => item.UserId == user.Id)
                        .Select(item => item.SchoolLevel).OrderBy(item => item).ToList()
                }).ToList()
        });
    }

    [HttpPut("{userId}")]
    public async Task<ActionResult<TeacherAccessUserDto>> Update(
        string userId,
        [FromBody] UpdateTeacherAccessDto model)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null || !await userManager.IsInRoleAsync(user, "Teacher")) return NotFound();

        var scopes = UserAccessService.NormalizeScopes(model.Scopes);
        if (scopes.Count == 0)
            return BadRequest(new { Message = "Select at least one access scope." });

        var homerooms = UserAccessService.NormalizeHomerooms(model.Homerooms);
        var schoolLevels = UserAccessService.NormalizeSchoolLevels(model.SchoolLevels);
        if (scopes.Contains(TeacherAccessScopes.Class) && homerooms.Count == 0)
            return BadRequest(new { Message = "Select at least one class for class access." });

        if (scopes.Contains(TeacherAccessScopes.Class))
        {
            var availableHomerooms = await context.StudentProfiles.AsNoTracking()
                .Where(item => item.Homeroom != null && item.Homeroom != "")
                .Select(item => item.Homeroom!).Distinct().ToListAsync();
            if (homerooms.Except(availableHomerooms, StringComparer.OrdinalIgnoreCase).Any())
                return BadRequest(new { Message = "One or more selected classes are invalid." });
        }
        if (scopes.Contains(TeacherAccessScopes.SchoolLevel) && schoolLevels.Count == 0)
            return BadRequest(new { Message = "Select at least one school level for school-level access." });

        var existingGrants = await context.TeacherAccessGrants.Where(item => item.UserId == userId).ToListAsync();
        var existingClasses = await context.TeacherClassAccesses.Where(item => item.UserId == userId).ToListAsync();
        var existingSchoolLevels = await context.TeacherSchoolLevelAccesses
            .Where(item => item.UserId == userId).ToListAsync();
        context.TeacherAccessGrants.RemoveRange(existingGrants);
        context.TeacherClassAccesses.RemoveRange(existingClasses);
        context.TeacherSchoolLevelAccesses.RemoveRange(existingSchoolLevels);
        context.TeacherAccessGrants.AddRange(scopes.Select(scope => new TeacherAccessGrant
        {
            UserId = userId,
            Scope = scope
        }));
        if (scopes.Contains(TeacherAccessScopes.Class))
        {
            context.TeacherClassAccesses.AddRange(homerooms.Select(homeroom => new TeacherClassAccess
            {
                UserId = userId,
                Homeroom = homeroom
            }));
        }
        if (scopes.Contains(TeacherAccessScopes.SchoolLevel))
        {
            context.TeacherSchoolLevelAccesses.AddRange(schoolLevels.Select(schoolLevel =>
                new TeacherSchoolLevelAccess
                {
                    UserId = userId,
                    SchoolLevel = schoolLevel
                }));
        }
        await context.SaveChangesAsync();

        return Ok(new TeacherAccessUserDto
        {
            UserId = user.Id,
            Username = user.UserName!,
            Email = user.Email,
            FullName = user.FullName,
            Scopes = scopes,
            Homerooms = scopes.Contains(TeacherAccessScopes.Class) ? homerooms : [],
            SchoolLevels = scopes.Contains(TeacherAccessScopes.SchoolLevel) ? schoolLevels : []
        });
    }
}
