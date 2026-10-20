using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OlympiaStudent360.CanvasEtl;

internal sealed partial class CanvasApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly CanvasEtlOptions _options;

    public CanvasApiClient(HttpClient httpClient, CanvasEtlOptions options)
    {
        _httpClient = httpClient;
        _options = options;
        // The database retry recreates this wrapper with the same HttpClient. Settings
        // cannot be assigned again after that client has sent its first request.
        if (_httpClient.BaseAddress != options.BaseUri) _httpClient.BaseAddress = options.BaseUri;
        if (_httpClient.Timeout != TimeSpan.FromMinutes(2)) _httpClient.Timeout = TimeSpan.FromMinutes(2);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.AccessToken);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any(item => item.Product?.Name == "OlympiaStudent360-CanvasETL"))
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "OlympiaStudent360-CanvasETL/1.0 (+https://theolympiaschools.edu.vn)");
        if (!_httpClient.DefaultRequestHeaders.Accept.Any(item => item.MediaType == "application/json"))
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<CanvasCourseDto>> GetCourses(CancellationToken cancellationToken)
    {
        var scope = _options.IncludeAllCourses
            ? "&state[]=all"
            : _options.IncludeCompletedCourses
                ? "&with_enrollments=true&enrollment_type[]=student&published=true&state[]=available&state[]=completed"
                : "&with_enrollments=true&enrollment_type[]=student&published=true&state[]=available";
        // Even an all-state import must stay inside the explicitly configured terms. The old
        // implementation silently dropped the term filter when IncludeAllCourses was enabled,
        // which could turn a current-year rebuild into an account-wide historical import.
        var termIds = _options.EnrollmentTermIds.Count == 0
            ? new long?[] { null }
            : _options.EnrollmentTermIds.Select(id => (long?)id).ToArray();
        var courses = new Dictionary<long, CanvasCourseDto>();
        foreach (var termId in termIds)
        {
            var termFilter = termId is null ? "" : $"&enrollment_term_id={termId}";
            var page = await GetAll<CanvasCourseDto>(
                $"api/v1/accounts/{Escape(_options.AccountId)}/courses" +
                $"?per_page={_options.PerPage}&include[]=term{scope}{termFilter}",
                cancellationToken);
            foreach (var course in page)
            {
                courses[course.Id] = course;
            }

            if (!_options.IncludeAllCourses)
            {
                // The main filtered query cannot surface courses deleted after an earlier sync.
                // Read tombstones separately so reconciliation can remove stale warehouse rows.
                var deleted = await GetAll<CanvasCourseDto>(
                    $"api/v1/accounts/{Escape(_options.AccountId)}/courses" +
                    $"?per_page={_options.PerPage}&include[]=term&state[]=deleted{termFilter}",
                    cancellationToken);
                foreach (var course in deleted)
                {
                    courses[course.Id] = course;
                }
            }
        }

        return courses.Values.OrderBy(course => course.Id).ToList();
    }

    public Task<List<CanvasUserDto>> GetStudents(CancellationToken cancellationToken) => GetAll<CanvasUserDto>(
        $"api/v1/accounts/{Escape(_options.AccountId)}/users" +
        $"?per_page={_options.PerPage}&enrollment_type=student&sort=id",
        cancellationToken);

    public async Task<List<CanvasAccountDto>> GetAccounts(CancellationToken cancellationToken)
    {
        var root = await GetOne<CanvasAccountDto>(
            $"api/v1/accounts/{Escape(_options.AccountId)}", cancellationToken);
        var descendants = await GetAll<CanvasAccountDto>(
            $"api/v1/accounts/{Escape(_options.AccountId)}/sub_accounts" +
            $"?recursive=true&per_page={_options.PerPage}", cancellationToken);
        return descendants.Append(root).GroupBy(account => account.Id)
            .Select(group => group.First()).OrderBy(account => account.Id).ToList();
    }

    public Task<List<CanvasCourseSectionDto>> GetSections(
        long courseId,
        CancellationToken cancellationToken) => GetAll<CanvasCourseSectionDto>(
        $"api/v1/courses/{courseId}/sections?per_page={_options.PerPage}&include[]=total_students",
        cancellationToken);

    public Task<List<CanvasAssignmentDto>> GetAssignments(long courseId, CancellationToken cancellationToken) =>
        GetAll<CanvasAssignmentDto>($"api/v1/courses/{courseId}/assignments?per_page={_options.PerPage}", cancellationToken);

    public Task<List<CanvasAssignmentGroupDto>> GetAssignmentGroups(long courseId, CancellationToken cancellationToken) =>
        GetAll<CanvasAssignmentGroupDto>($"api/v1/courses/{courseId}/assignment_groups?per_page={_options.PerPage}", cancellationToken);

    public async Task<List<CanvasGradingPeriodDto>> GetGradingPeriods(
        long courseId,
        CancellationToken cancellationToken)
    {
        var results = new List<CanvasGradingPeriodDto>();
        Uri? next = new(_options.BaseUri,
            $"api/v1/courses/{courseId}/grading_periods?per_page={_options.PerPage}");
        while (next is not null)
        {
            EnsureTrustedCanvasUri(next);
            using var response = await SendWithRetry(next, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<CanvasGradingPeriodsDto>(
                stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Canvas returned an empty JSON response for {next.AbsolutePath}.");
            results.AddRange(page.GradingPeriods);
            next = NextPage(response);
        }

        return results.OrderBy(period => period.StartDate).ThenBy(period => period.Id).ToList();
    }

    public Task<List<CanvasEnrollmentDto>> GetEnrollments(
        long courseId,
        CancellationToken cancellationToken,
        long? gradingPeriodId = null) => GetAll<CanvasEnrollmentDto>(
        $"api/v1/courses/{courseId}/enrollments?per_page={_options.PerPage}" +
        "&type[]=StudentEnrollment&state[]=active&state[]=completed&include[]=current_points" +
        (gradingPeriodId is null ? "" : $"&grading_period_id={gradingPeriodId.Value}"),
        cancellationToken);

    public async Task<List<CanvasSubmissionDto>> GetSubmissions(
        long courseId,
        IReadOnlyCollection<long> studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var submissions = new List<CanvasSubmissionDto>();
        // Keep URLs bounded while ensuring Canvas never returns records for former students.
        foreach (var studentBatch in studentIds.Distinct().Order().Chunk(50))
        {
            var studentFilter = string.Concat(studentBatch.Select(id => $"&student_ids[]={id}"));
            var path = $"api/v1/courses/{courseId}/students/submissions?per_page={_options.PerPage}" +
                $"{studentFilter}&include[]=user&state_based_on_date=false";
            if (_options.SinceUtc is null)
            {
                submissions.AddRange(await GetAll<CanvasSubmissionDto>(path, cancellationToken));
                continue;
            }

            // Canvas applies submitted_since and graded_since as separate filters. Query both so a
            // newly submitted attempt and a grade changed on an older attempt are both captured.
            var cutoffValue = _options.SinceUtc.Value.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            var cutoff = Uri.EscapeDataString(cutoffValue);
            var submittedTask = GetAll<CanvasSubmissionDto>(
                $"{path}&submitted_since={cutoff}", cancellationToken);
            var gradedTask = GetAll<CanvasSubmissionDto>(
                $"{path}&graded_since={cutoff}", cancellationToken);
            await Task.WhenAll(submittedTask, gradedTask);
            submissions.AddRange(await submittedTask);
            submissions.AddRange(await gradedTask);
        }

        return submissions
            .GroupBy(SubmissionKey)
            .Select(group => group.OrderByDescending(item => item.GradedAt ?? item.SubmittedAt).First())
            .ToList();
    }

    private static string SubmissionKey(CanvasSubmissionDto submission) => submission.Id is { } id
        ? $"id:{id}"
        : $"course-assignment-user:{submission.AssignmentId}:{submission.UserId}";

    private async Task<List<T>> GetAll<T>(string path, CancellationToken cancellationToken)
    {
        var results = new List<T>();
        Uri? next = new(_options.BaseUri, path);

        while (next is not null)
        {
            EnsureTrustedCanvasUri(next);
            using var response = await SendWithRetry(next, cancellationToken);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Canvas returned an empty JSON response for {next.AbsolutePath}.");
            results.AddRange(page);
            next = NextPage(response);
        }

        return results;
    }

    private async Task<T> GetOne<T>(string path, CancellationToken cancellationToken)
    {
        var uri = new Uri(_options.BaseUri, path);
        EnsureTrustedCanvasUri(uri);
        using var response = await SendWithRetry(uri, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Canvas returned an empty JSON response for {uri.AbsolutePath}.");
    }

    private async Task<HttpResponseMessage> SendWithRetry(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(
                    uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < 6)
            {
                var timeoutDelay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt)));
                Console.WriteLine(
                    $"Canvas request timed out; retrying in {timeoutDelay.TotalSeconds:0} seconds " +
                    $"(attempt {attempt}/6).");
                await Task.Delay(timeoutDelay, cancellationToken);
                continue;
            }
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (attempt >= 6 || (response.StatusCode != HttpStatusCode.TooManyRequests &&
                (int)response.StatusCode < 500))
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                throw new HttpRequestException(
                    $"Canvas request {uri.AbsolutePath} failed with HTTP {(int)response.StatusCode}: {Trim(body, 500)}");
            }

            var delay = RetryDelay(response, attempt);
            Console.WriteLine(
                $"Canvas returned HTTP {(int)response.StatusCode}; retrying in {delay.TotalSeconds:0} seconds (attempt {attempt}/6).");
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero
                ? (delay > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : delay)
                : TimeSpan.FromSeconds(1);
        }

        return TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, attempt)));
    }

    private Uri? NextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var value in values.SelectMany(value => value.Split(',')))
        {
            var match = LinkPattern().Match(value);
            if (match.Success && match.Groups[2].Value.Equals("next", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(match.Groups[1].Value, UriKind.Absolute);
                EnsureTrustedCanvasUri(uri);
                return uri;
            }
        }

        return null;
    }

    private void EnsureTrustedCanvasUri(Uri uri)
    {
        if (!uri.Scheme.Equals(_options.BaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals(_options.BaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != _options.BaseUri.Port)
        {
            throw new InvalidOperationException("Canvas pagination returned a URL on an unexpected origin.");
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string Trim(string value, int length) => value.Length <= length ? value : value[..length];

    [GeneratedRegex("<([^>]+)>\\s*;\\s*rel=\\\"?([^\\\";\\s]+)\\\"?", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();
}
