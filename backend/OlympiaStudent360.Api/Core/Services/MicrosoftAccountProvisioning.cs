using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class MicrosoftAccountProvisioning(
    UserManager<ApplicationUser> users, MicrosoftSignInSettings settings, MicrosoftDirectory directory)
{
    public bool Enabled => settings.Enabled;

    // The account is already saved. A directory outage leaves its link pending for the backfill.
    internal async Task<string?> LinkSavedEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var email = user.Email?.Trim();
        if (!settings.Enabled || user.MicrosoftSignInBlocked || !user.EmailConfirmed || string.IsNullOrEmpty(email))
            return null;
        if ((await users.GetLoginsAsync(user)).Any(login => login.LoginProvider == MicrosoftSignIn.Scheme))
            return null;
        if (email.Length > 256 || !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            return "microsoft_invalid_email";
        if ((await users.GetRolesAsync(user)).Count == 0)
            return null;

        try
        {
            var member = await directory.ResolveMemberAsync(email, ct);
            if (member is null) return "microsoft_no_match";
            var uniqueAccount = await MicrosoftSignIn.FindUniqueAccountAsync(users, member.Addresses, ct);
            if (uniqueAccount?.Id != user.Id) return "microsoft_ambiguous";

            var key = MicrosoftSignIn.ProviderKey(settings.TenantId, member.Id);
            var result = await users.AddLoginAsync(user, new UserLoginInfo(MicrosoftSignIn.Scheme, key, "Microsoft"));
            return result.Succeeded ? "microsoft_linked" : "microsoft_conflict";
        }
        catch (MicrosoftDirectoryException error)
        {
            return error.Code;
        }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
        {
            return "microsoft_conflict";
        }
    }
}
