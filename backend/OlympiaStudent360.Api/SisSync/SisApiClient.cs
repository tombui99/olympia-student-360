using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace OlympiaStudent360.Api.SisSync;

/// <summary>Never includes response bodies, credentials, filter values or tokens in exceptions.</summary>
public sealed class SisApiException(string message) : Exception(message);
public sealed record SisPage(IReadOnlyList<JsonElement> Records, string Watermark);

public sealed class SisApiClient(HttpClient http, SisEtlOptions options)
{
    private string? accessToken;
    private DateTimeOffset refreshAt;
    private DateTimeOffset nextRequestAt;
    private HashSet<string> scopes = [];

    public async Task CheckScopesAsync(CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        foreach (var endpoint in SisEndpoints.All.Where(x => options.Resources.Contains(x.Name)))
            if (!scopes.Contains(endpoint.Scope))
                throw new SisApiException($"SIS client is missing required scope {endpoint.Scope} for {endpoint.Name}.");
    }

    public async IAsyncEnumerable<SisPage> ReadAsync(string endpoint, IReadOnlyDictionary<string, string> filters,
        string? since, [EnumeratorCancellation] CancellationToken ct)
    {
        string? cursor = null;
        string? watermark = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var query = new SortedDictionary<string, string>(filters.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal)
            { ["limit"] = options.PageSize.ToString(CultureInfo.InvariantCulture) };
            if (cursor is not null) query["cursor"] = cursor;
            else if (since is not null) query["updated_since"] = since;
            var path = "api/v1/" + endpoint + "?" + string.Join("&", query.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
            using var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, new Uri(options.BaseUri, path)), true, endpoint, ct);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("meta", out var meta) ||
                !meta.TryGetProperty("watermark", out var wm) || wm.ValueKind != JsonValueKind.String ||
                !TryTimestamp(wm.GetString(), out _) ||
                !meta.TryGetProperty("has_more", out var more) || more.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new SisApiException($"Invalid SIS list envelope for {endpoint}.");
            if (meta.TryGetProperty("api_version", out var version) && version.GetString() != "1.0")
                throw new SisApiException("Unsupported SIS API version.");
            var pageWatermark = wm.GetString()!;
            if (watermark is not null && watermark != pageWatermark)
                throw new SisApiException($"SIS watermark changed during pagination for {endpoint}.");
            watermark = pageWatermark;
            if (since is not null && DateTimeOffset.Parse(watermark, CultureInfo.InvariantCulture) < DateTimeOffset.Parse(since, CultureInfo.InvariantCulture))
                throw new SisApiException("SIS watermark moved backwards.");
            cursor = more.GetBoolean() ? meta.GetProperty("next_cursor").GetString() : null;
            if (more.GetBoolean() && (string.IsNullOrEmpty(cursor) || !seenCursors.Add(cursor)))
                throw new SisApiException($"Invalid or repeated SIS cursor for {endpoint}.");
            yield return new SisPage(data.EnumerateArray().Select(x => x.Clone()).ToArray(), watermark);
        } while (cursor is not null);
    }

    internal static bool TryTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return value is not null && value.Contains('T') &&
            (value.EndsWith('Z') || value.LastIndexOf('+') > 10 || value.LastIndexOf('-') > 10) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (accessToken is not null && DateTimeOffset.UtcNow < refreshAt) return;
        using var response = await SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUri, "oauth/token"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(options.ClientId + ":" + options.ClientSecret)));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
            return request;
        }, false, "oauth/token", ct);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = json.RootElement;
        if (!root.TryGetProperty("access_token", out var token) || token.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.GetString()) ||
            !root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out var seconds) || seconds <= 0 ||
            !root.TryGetProperty("token_type", out var type) || !string.Equals(type.GetString(), "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new SisApiException("Invalid SIS token response.");
        accessToken = token.GetString();
        refreshAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, seconds - 60));
        scopes = (root.TryGetProperty("scope", out var scope) ? scope.GetString() ?? "" : "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, bool bearer, string endpoint, CancellationToken ct)
    {
        var refreshed = false;
        for (var attempt = 0; ; attempt++)
        {
            if (bearer) await EnsureTokenAsync(ct);
            var wait = nextRequestAt - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            using var request = create();
            if (bearer) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("OlympiaStudent360-SIS-ETL/1.0");
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));
            HttpResponseMessage response;
            try
            {
                nextRequestAt = DateTimeOffset.UtcNow.AddMilliseconds(options.RequestIntervalMilliseconds);
                response = await http.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= 5) throw new SisApiException($"SIS network request failed after retries for {endpoint}.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), ct);
                continue;
            }
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0" &&
                    response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out var unix))
                {
                    var resetAt = DateTimeOffset.FromUnixTimeSeconds(unix).AddSeconds(1);
                    if (resetAt > nextRequestAt) nextRequestAt = resetAt;
                }
                return response;
            }
            var status = response.StatusCode;
            var requestId = response.Headers.TryGetValues("X-Request-Id", out var ids) ? ids.FirstOrDefault() : null;
            // Restrict diagnostic values to the vendor's documented request-id alphabet.
            requestId = requestId is { Length: >= 8 and <= 64 } && requestId.All(c => char.IsAsciiLetterOrDigit(c) || "._:-".Contains(c)) ? requestId : "unavailable";
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
            response.Dispose();
            if (status == HttpStatusCode.Unauthorized && bearer && !refreshed)
            {
                refreshed = true;
                accessToken = null;
                continue;
            }
            if ((status is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError) && attempt < 5)
            {
                var delay = retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, ct);
                continue;
            }
            throw new SisApiException($"SIS {endpoint} returned HTTP {(int)status}; request_id={requestId}.");
        }
    }
}
