using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.Net.Mail;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/users/{userId}/microsoft")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminMicrosoftSignInController(
    UserManager<ApplicationUser> users, MicrosoftSignInSettings settings, MicrosoftDirectory directory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string userId)
    {
        if (!settings.Enabled) return Ok(new { Enabled = false, Linked = false });
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();

        return Ok(new { Enabled = true, Linked = await FindLoginAsync(user) is not null,
            Blocked = user.MicrosoftSignInBlocked, settings.AutoLinkByEmail });
    }

    [HttpPut]
    public async Task<IActionResult> Link(string userId, [FromBody] MicrosoftAccountLink link, CancellationToken ct)
    {
        if (!settings.Enabled) return NotFound();
        var user = await users.FindByIdAsync(userId);
        if (user is null) return NotFound();
        var email = user.Email?.Trim();
        if (email is not { Length: > 0 and <= 256 } || !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(email, address.Address, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { Code = "microsoft_invalid_email" });
        if (!string.Equals(email, link.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
            return Conflict(new { Code = "microsoft_email_changed" });

        if (await FindLoginAsync(user) is not null)
            return user.MicrosoftSignInBlocked ? Conflict() : NoContent();

        try
        {
            var objectId = await directory.FindMemberAsync(email, ct);
            if (objectId is null) return BadRequest(new { Code = "microsoft_no_match" });
            var key = MicrosoftSignIn.ProviderKey(settings.TenantId, objectId.Value);
            user.MicrosoftSignInBlocked = false;
            var result = await users.AddLoginAsync(user, new UserLoginInfo(MicrosoftSignIn.Scheme, key, "Microsoft"));
            return result.Succeeded ? NoContent() : Conflict();
        }
        catch (MicrosoftDirectoryException error)
        {
            var response = new { error.Code };
            return error.Code == "microsoft_ambiguous" ? Conflict(response) : StatusCode(503, response);
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
        if (user.MicrosoftSignInBlocked && existing is null) return NoContent();
        user.MicrosoftSignInBlocked = true;
        // Identity saves the block together with the login removal and security stamp.
        var result = existing is null ? await users.UpdateAsync(user)
            : await users.RemoveLoginAsync(user, existing.LoginProvider, existing.ProviderKey);
        return result.Succeeded ? NoContent() : Conflict();
    }

    private async Task<UserLoginInfo?> FindLoginAsync(ApplicationUser user) =>
        (await users.GetLoginsAsync(user)).SingleOrDefault(login =>
            login.LoginProvider == MicrosoftSignIn.Scheme &&
            login.ProviderKey.StartsWith($"{settings.TenantId:D}:", StringComparison.Ordinal));

    public sealed record MicrosoftAccountLink(string? Email);
}
