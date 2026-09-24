using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public static class TeacherAccessScopes
{
    public const string All = "All";
    public const string Health = "Health";
    public const string Bus = "Bus";
    public const string Academic = "Academic";
    public const string Class = "Class";

    public static readonly string[] Values = [All, Health, Bus, Academic, Class];

    public static bool IsValid(string value) =>
        Values.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string? Canonical(string? value) => Values.FirstOrDefault(scope =>
        string.Equals(scope, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record UserAccessSnapshot(
    string? UserId,
    string Role,
    IReadOnlySet<string> Scopes,
    IReadOnlyList<string> Homerooms)
{
    public bool IsTeacher => string.Equals(Role, "Teacher", StringComparison.OrdinalIgnoreCase);
    public bool HasAll => !IsTeacher || Scopes.Contains(TeacherAccessScopes.All);
    public bool CanAccessHealth => HasAll || Scopes.Contains(TeacherAccessScopes.Health);
    public bool CanAccessBus => HasAll || Scopes.Contains(TeacherAccessScopes.Bus);
    public bool CanAccessAcademic => HasAll || Scopes.Contains(TeacherAccessScopes.Academic) ||
        Scopes.Contains(TeacherAccessScopes.Class);
    public bool CanAccessWorkspace => CanAccessAcademic;
    public bool CanAccessAtRisk => CanAccessAcademic;
    public bool CanAccessCommendations => CanAccessAcademic;
    public bool RestrictsClasses => IsTeacher && !HasAll && Scopes.Contains(TeacherAccessScopes.Class);
    public string CacheKey => !IsTeacher
        ? $"role:{Role.ToLowerInvariant()}"
        : $"teacher:{string.Join(',', Scopes.Order())}:{string.Join(',', Homerooms.Order())}";

    public bool CanAccessModule(string moduleId) => moduleId switch
    {
        "student-health-visits" or "student-mental-health" => CanAccessHealth,
        "student-bus-attendance" => CanAccessBus,
        "student-behaviours" or "student-projects" => CanAccessAcademic,
        _ => HasAll
    };

    public bool CanAccessMetric(string metricId, string? source = null, string? category = null)
    {
        if (HasAll) return true;
        var classification = $"{metricId} {source} {category}";
        if (metricId is "mental-health-records" or "high-risk-mental-health" or "health-visit-records" ||
            classification.Contains("health", StringComparison.OrdinalIgnoreCase) ||
            classification.Contains("wellbeing", StringComparison.OrdinalIgnoreCase) ||
            classification.Contains("well-being", StringComparison.OrdinalIgnoreCase) ||
            classification.Contains("medical", StringComparison.OrdinalIgnoreCase) ||
            classification.Contains("clinic", StringComparison.OrdinalIgnoreCase))
            return CanAccessHealth;
        if (metricId.StartsWith("bus-", StringComparison.Ordinal) ||
            classification.Contains("bus", StringComparison.OrdinalIgnoreCase) ||
            classification.Contains("transport", StringComparison.OrdinalIgnoreCase))
            return CanAccessBus;
        return CanAccessAcademic;
    }
}

public sealed class UserAccessService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
{
    public const string HttpContextItemKey = "olympia-user-access";

    public UserAccessSnapshot Current =>
        httpContextAccessor.HttpContext?.Items[HttpContextItemKey] as UserAccessSnapshot
        ?? new UserAccessSnapshot(null, "Teacher",
            new HashSet<string>([TeacherAccessScopes.All], StringComparer.OrdinalIgnoreCase), []);

    public async Task<UserAccessSnapshot> LoadAsync(ClaimsPrincipal principal)
    {
        var role = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).FirstOrDefault()
            ?? principal.FindFirst("role")?.Value
            ?? "Teacher";
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return await LoadAsync(userId, role);
    }

    public async Task<UserAccessSnapshot> LoadAsync(string? userId, string role)
    {
        if (!string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(userId))
        {
            return new UserAccessSnapshot(userId, role,
                new HashSet<string>([TeacherAccessScopes.All], StringComparer.OrdinalIgnoreCase), []);
        }

        var scopes = await context.TeacherAccessGrants.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Scope)
            .ToListAsync();
        if (scopes.Count == 0) scopes.Add(TeacherAccessScopes.All);
        var canonicalScopes = scopes.Select(TeacherAccessScopes.Canonical).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (canonicalScopes.Count == 0) canonicalScopes.Add(TeacherAccessScopes.All);

        var homerooms = await context.TeacherClassAccesses.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Homeroom)
            .OrderBy(item => item)
            .ToListAsync();
        return new UserAccessSnapshot(userId, role, canonicalScopes, homerooms);
    }

    public IQueryable<StudentProfile> ApplyStudentScope(IQueryable<StudentProfile> query)
    {
        var access = Current;
        if (!access.RestrictsClasses) return query;
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(StudentProfile), "student");
        var homeroom = System.Linq.Expressions.Expression.Property(parameter, nameof(StudentProfile.Homeroom));
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<StudentProfile, bool>>(
            HomeroomMatch(homeroom, access.Homerooms), parameter));
    }

    public IQueryable<T> ApplyStudentScope<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, StudentProfile?>> studentSelector)
    {
        var access = Current;
        if (!access.RestrictsClasses) return query;
        var parameter = studentSelector.Parameters[0];
        var student = studentSelector.Body;
        var homeroom = System.Linq.Expressions.Expression.Property(student, nameof(StudentProfile.Homeroom));
        var notNull = System.Linq.Expressions.Expression.NotEqual(
            student, System.Linq.Expressions.Expression.Constant(null, typeof(StudentProfile)));
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.AndAlso(
                notNull, HomeroomMatch(homeroom, access.Homerooms)), parameter));
    }

    public IQueryable<T> ApplyHomeroomScope<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, string?>> homeroomSelector)
    {
        var access = Current;
        if (!access.RestrictsClasses) return query;
        return query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            HomeroomMatch(homeroomSelector.Body, access.Homerooms),
            homeroomSelector.Parameters));
    }

    private static System.Linq.Expressions.Expression HomeroomMatch(
        System.Linq.Expressions.Expression homeroom,
        IReadOnlyList<string> allowedHomerooms)
    {
        // MySQL's EF provider cannot reliably type-map captured primitive collections. Build a
        // normal SQL equality disjunction (Homeroom = '10A' OR Homeroom = '10B') instead of IN
        // over a runtime array. A teacher with no assigned classes safely matches no students.
        System.Linq.Expressions.Expression? match = null;
        foreach (var allowedHomeroom in allowedHomerooms)
        {
            var equals = System.Linq.Expressions.Expression.Equal(
                homeroom,
                System.Linq.Expressions.Expression.Constant(allowedHomeroom, typeof(string)));
            match = match == null
                ? equals
                : System.Linq.Expressions.Expression.OrElse(match, equals);
        }
        return match ?? System.Linq.Expressions.Expression.Constant(false);
    }

    public static List<string> NormalizeScopes(IEnumerable<string>? scopes)
    {
        var normalized = (scopes ?? []).Select(TeacherAccessScopes.Canonical).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return normalized.Contains(TeacherAccessScopes.All, StringComparer.OrdinalIgnoreCase)
            ? [TeacherAccessScopes.All]
            : normalized;
    }

    public static List<string> NormalizeHomerooms(IEnumerable<string>? homerooms) =>
        (homerooms ?? []).Select(item => item.Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
}

public sealed class UserAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, UserAccessService accessService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var access = await accessService.LoadAsync(context.User);
            context.Items[UserAccessService.HttpContextItemKey] = access;
            if (access.IsTeacher && !CanAccessPath(context.Request.Path, access))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "access_scope_updated",
                    message = "Your data access does not include this area. Your access may have been updated."
                });
                return;
            }
        }

        await next(context);
    }

    private static bool CanAccessPath(PathString requestPath, UserAccessSnapshot access)
    {
        var path = requestPath.Value?.ToLowerInvariant() ?? "";
        if (!path.StartsWith("/api/")) return true;

        if (path.StartsWith("/api/transcripts") || path.StartsWith("/api/dashboard/canvas-explore"))
            return access.CanAccessAcademic && access.CanAccessWorkspace;
        if (path.StartsWith("/api/reports/health-visits")) return access.CanAccessHealth;
        if (path.StartsWith("/api/reports/custom") || path.StartsWith("/api/workspace-ai-reports") ||
            path.StartsWith("/api/users/directory")) return access.CanAccessWorkspace;
        if (path.StartsWith("/api/dashboard/metrics") || path.StartsWith("/api/dashboard/metric-drilldown"))
            return access.CanAccessWorkspace;
        if (path.StartsWith("/api/customforms") || path.StartsWith("/api/custom-forms"))
            return access.CanAccessAcademic;
        if (path.StartsWith("/api/students/at-risk") || path.StartsWith("/api/students/commendations") ||
            path.StartsWith("/api/students/classification-settings")) return access.CanAccessAcademic;
        if (path.StartsWith("/api/students/") && path.Contains("/canvas/")) return access.CanAccessAcademic;
        if (path.StartsWith("/api/students/") && (path.EndsWith("/behaviours") ||
            path.EndsWith("/projects") || path.Contains("/parents/"))) return access.CanAccessAcademic;
        if (path.StartsWith("/api/students/") && (path.EndsWith("/mental-health") ||
            path.EndsWith("/health-visits"))) return access.CanAccessHealth;
        if (path.StartsWith("/api/students/") && path.EndsWith("/bus-attendance")) return access.CanAccessBus;

        var moduleId = ModuleFromPath(path, "/api/module-records/") ??
            ModuleFromPath(path, "/api/imports/modules/") ??
            ModuleFromPath(path, "/api/imports/");
        return moduleId == null || access.CanAccessModule(moduleId);
    }

    private static string? ModuleFromPath(string path, string prefix)
    {
        if (!path.StartsWith(prefix)) return null;
        return path[prefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }
}
