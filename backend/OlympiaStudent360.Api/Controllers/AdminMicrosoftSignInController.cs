using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users/{userId}/microsoft")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminMicrosoftSignInController(
    UserManager<ApplicationUser> users, MicrosoftSignInSettings settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string userId)
    {
        if (!settings.Enabled) return Ok(new { Enabled = false, ObjectId = (Guid?)null });
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var login = await FindLoginAsync(user);
        var objectId = login is null ? (Guid?)null : Guid.Parse(login.ProviderKey.Split(':')[1]);
        return Ok(new { Enabled = true, ObjectId = objectId });
    }

    [HttpPut]
    public async Task<IActionResult> Link(string userId, [FromBody] MicrosoftAccountLink link)
    {
        if (!settings.Enabled) return NotFound();
        if (link.ObjectId == Guid.Empty) return BadRequest();
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(user.Email)) return BadRequest();

        var key = MicrosoftSignIn.ProviderKey(settings.TenantId, link.ObjectId);
        var existing = await FindLoginAsync(user);
        if (existing is not null) return existing.ProviderKey == key ? NoContent() : Conflict();

        try
        {
            var result = await users.AddLoginAsync(user, new UserLoginInfo(MicrosoftSignIn.Scheme, key, "Microsoft"));
            return result.Succeeded ? NoContent() : Conflict();
        }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict();
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Unlink(string userId)
    {
        if (!settings.Enabled) return NotFound();
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var existing = await FindLoginAsync(user);
        if (existing is null) return NoContent();
        // Identity updates the security stamp as part of removing a login.
        var result = await users.RemoveLoginAsync(user, existing.LoginProvider, existing.ProviderKey);
        return result.Succeeded ? NoContent() : Conflict();
    }

    private async Task<UserLoginInfo?> FindLoginAsync(ApplicationUser user) =>
        (await users.GetLoginsAsync(user)).SingleOrDefault(login =>
            login.LoginProvider == MicrosoftSignIn.Scheme &&
            login.ProviderKey.StartsWith($"{settings.TenantId:D}:", StringComparison.Ordinal));

    public sealed record MicrosoftAccountLink(Guid ObjectId);
}
