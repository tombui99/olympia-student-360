using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class MicrosoftDirectory(HttpClient http, MicrosoftSignInSettings settings)
{
    private const string GraphUsers = "https://graph.microsoft.com/v1.0/users";
    private const string Fields = "$select=id,mail,userPrincipalName,userType";

    internal async Task<string[]> GetMemberAddressesAsync(Guid objectId, CancellationToken ct)
    {
        var user = await QueryAsync<DirectoryUser>($"{GraphUsers}/{objectId:D}?{Fields}", ct);
        return user is { UserType: "Member" } && user.Id == objectId
            ? new[] { user.Mail, user.UserPrincipalName }
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Select(address => address!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    internal async Task<Guid?> FindMemberAsync(string email, CancellationToken ct)
    {
        // Escape the OData string literal before encoding the query parameter.
        var literal = "'" + email.Replace("'", "''") + "'";
        var filter = Uri.EscapeDataString($"mail eq {literal} or userPrincipalName eq {literal}");
        var matches = await QueryAsync<UserResults>($"{GraphUsers}?{Fields}&$filter={filter}&$top=2", ct);
        if (matches?.Value is null)
            throw new MicrosoftDirectoryException("microsoft_directory_unavailable");

        // A second page means the response has not established a unique match.
        if (matches.Value.Length > 1 || !string.IsNullOrEmpty(matches.NextLink))
            throw new MicrosoftDirectoryException("microsoft_ambiguous");

        var user = matches.Value.SingleOrDefault();
        return user is { UserType: "Member" } && user.Id != Guid.Empty &&
            (string.Equals(user.Mail, email, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(user.UserPrincipalName, email, StringComparison.OrdinalIgnoreCase))
            ? user.Id : null;
    }

    private async Task<T?> QueryAsync<T>(string url, CancellationToken ct) where T : class
    {
        try
        {
            using var credentials = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId.ToString("D"),
                ["client_secret"] = settings.ClientSecret,
                ["grant_type"] = "client_credentials",
                ["scope"] = "https://graph.microsoft.com/.default"
            });
            using var tokenResponse = await http.PostAsync(
                $"https://login.microsoftonline.com/{settings.TenantId:D}/oauth2/v2.0/token", credentials, ct);
            tokenResponse.EnsureSuccessStatusCode();
            var token = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw new MicrosoftDirectoryException("microsoft_directory_unavailable");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new MicrosoftDirectoryException("microsoft_directory_permission");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct)
                ?? throw new MicrosoftDirectoryException("microsoft_directory_unavailable");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException ||
            error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new MicrosoftDirectoryException("microsoft_directory_unavailable");
        }
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string? AccessToken);
    private sealed record UserResults(DirectoryUser[]? Value, [property: JsonPropertyName("@odata.nextLink")] string? NextLink);
    private sealed record DirectoryUser(Guid Id, string? Mail, string? UserPrincipalName, string? UserType);
}

internal sealed class MicrosoftDirectoryException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}
