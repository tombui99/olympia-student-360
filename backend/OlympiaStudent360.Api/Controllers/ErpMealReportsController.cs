using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/student-meals")]
public sealed class ErpMealReportsController(AppDbContext db, UserAccessService access) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<ErpMealReportOptionsDto>> Options(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        if (!ValidRange(from, to)) return BadRequest("Invalid date range.");
        if (!await db.ErpMealReportBuilds.AsNoTracking().AnyAsync(cancellationToken))
            return StatusCode(503, new { Message = "Meal report aggregates are being prepared." });
        var query = ScopedRows(from, to);
        return Ok(new ErpMealReportOptionsDto
        {
            Grades = await query.Where(x => x.Grade != null && x.Grade != "")
                .Select(x => x.Grade!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken),
            Classes = await query.Where(x => x.ClassName != null && x.ClassName != "")
                .Select(x => x.ClassName!).Distinct().OrderBy(x => x).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ErpMealReportPageDto>> Summary(
        [FromQuery] string report = "ordered-no-ticket",
        [FromQuery] string groupBy = "student",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? meal = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? className = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool export = false,
        CancellationToken cancellationToken = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var mealFilter = NormalizeMealFilter(meal);
        if (report is not ("ordered-no-ticket" or "no-order" or "coffee-frequency") ||
            groupBy is not ("student" or "week" or "month" or "grade" or "class") ||
            mealFilter == "invalid" || !ValidRange(from, to))
            return BadRequest("Invalid meal report filters.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, export ? 1000 : 100);
        var build = await db.ErpMealReportBuilds.AsNoTracking()
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (build is null)
            return StatusCode(503, new { Message = "Meal report aggregates are being prepared." });

        var source = ScopedRows(from, to);
        source = mealFilter switch
        {
            "breakfast" => source.Where(x => x.Meal == "Sáng" || x.Meal == "Ăn sáng" || x.Meal == "Breakfast"),
            "lunch" => source.Where(x => x.Meal == "Trưa" || x.Meal == "Ăn trưa" || x.Meal == "Lunch"),
            "snack" => source.Where(x => x.Meal == "Nhẹ" || x.Meal == "Ăn nhẹ" || x.Meal == "Snack"),
            _ => source
        };
        if (!string.IsNullOrWhiteSpace(grade)) source = source.Where(x => x.Grade == grade.Trim());
        if (!string.IsNullOrWhiteSpace(className)) source = source.Where(x => x.ClassName == className.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100) return BadRequest("Search is too long.");
            source = source.Where(x => x.StudentName.Contains(term) || x.ErpStudentId.Contains(term));
        }

        var measures = source.Select(x => new
        {
            x.StudentKey, x.StudentProfileId, x.ErpStudentId, x.StudentName,
            x.Grade, x.ClassName, x.EventDay, x.WeekStart, x.MonthStart,
            Count = report == "ordered-no-ticket" ? x.OrderedNoTicketCount :
                report == "no-order" ? x.NoOrderCount : x.CoffeeLineCount,
            Units = report == "coffee-frequency" ? x.CoffeeUnitCount : 0
        }).Where(x => x.Count > 0);
        var totals = await measures.GroupBy(_ => 1).Select(group => new
        {
            Events = group.Sum(x => x.Count),
            Units = group.Sum(x => x.Units),
            Students = group.Select(x => x.StudentKey).Distinct().Count()
        }).SingleOrDefaultAsync(cancellationToken);

        IQueryable<ErpMealReportRowDto> grouped = groupBy switch
        {
            "week" => measures.GroupBy(x => x.WeekStart).Select(group => new ErpMealReportRowDto
            {
                Key = "",
                Label = "", PeriodStart = group.Key,
                Count = group.Sum(x => x.Count), Units = group.Sum(x => x.Units),
                StudentCount = group.Select(x => x.StudentKey).Distinct().Count()
            }),
            "month" => measures.GroupBy(x => x.MonthStart).Select(group => new ErpMealReportRowDto
            {
                Key = "",
                Label = "", PeriodStart = group.Key,
                Count = group.Sum(x => x.Count), Units = group.Sum(x => x.Units),
                StudentCount = group.Select(x => x.StudentKey).Distinct().Count()
            }),
            "grade" => measures.GroupBy(x => x.Grade).Select(group => new ErpMealReportRowDto
            {
                Key = group.Key ?? "—", Label = group.Key ?? "—",
                Count = group.Sum(x => x.Count), Units = group.Sum(x => x.Units),
                StudentCount = group.Select(x => x.StudentKey).Distinct().Count()
            }),
            "class" => measures.GroupBy(x => x.ClassName).Select(group => new ErpMealReportRowDto
            {
                Key = group.Key ?? "—", Label = group.Key ?? "—",
                Count = group.Sum(x => x.Count), Units = group.Sum(x => x.Units),
                StudentCount = group.Select(x => x.StudentKey).Distinct().Count()
            }),
            _ => measures.GroupBy(x => new { x.StudentKey, x.StudentProfileId,
                    x.ErpStudentId, x.StudentName, x.Grade, x.ClassName })
                .Select(group => new ErpMealReportRowDto
                {
                    Key = group.Key.StudentKey, Label = group.Key.StudentName,
                    StudentProfileId = group.Key.StudentProfileId,
                    ErpStudentId = group.Key.ErpStudentId,
                    Grade = group.Key.Grade, ClassName = group.Key.ClassName,
                    LatestDay = group.Max(x => x.EventDay),
                    Count = group.Sum(x => x.Count), Units = group.Sum(x => x.Units),
                    StudentCount = 1
                })
        };
        var totalCount = await grouped.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        grouped = groupBy is "week" or "month"
            ? grouped.OrderByDescending(x => x.PeriodStart)
            : grouped.OrderByDescending(x => x.Count).ThenBy(x => x.Label);
        var items = await grouped.Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        if (groupBy is "week" or "month")
        {
            foreach (var item in items)
            {
                item.Key = $"{groupBy}:{item.PeriodStart:yyyy-MM-dd}";
                item.Label = item.PeriodStart?.ToString(groupBy == "month" ? "MM/yyyy" : "dd/MM/yyyy") ?? "—";
            }
        }
        return Ok(new ErpMealReportPageDto
        {
            Items = items,
            Page = page, PageSize = pageSize, TotalCount = totalCount,
            TotalPages = totalPages, TotalEvents = totals?.Events ?? 0,
            TotalUnits = totals?.Units ?? 0, TotalStudents = totals?.Students ?? 0,
            BuiltAtUtc = build.CompletedAtUtc
        });
    }

    [HttpGet("details")]
    public async Task<ActionResult<ErpMealReportDetailsPageDto>> Details(
        [FromQuery] string report,
        [FromQuery] string groupBy,
        [FromQuery] string? key,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? meal = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? className = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var mealFilter = NormalizeMealFilter(meal);
        if (report is not ("ordered-no-ticket" or "no-order" or "coffee-frequency") ||
            groupBy is not ("student" or "week" or "month" or "grade" or "class") ||
            mealFilter == "invalid" || key is null || key.Length > 120 || !ValidRange(from, to))
            return BadRequest("Invalid meal report detail filters.");
        if (!await db.ErpMealReportBuilds.AsNoTracking().AnyAsync(cancellationToken))
            return StatusCode(503, new { Message = "Meal report aggregates are being prepared." });

        var query = access.ApplyStudentScope(db.ErpMealReportDetails.AsNoTracking(), x => x.Student)
            .Where(x => x.Report == report);
        query = mealFilter switch
        {
            "breakfast" => query.Where(x => x.Meal == "Sáng" || x.Meal == "Ăn sáng" || x.Meal == "Breakfast"),
            "lunch" => query.Where(x => x.Meal == "Trưa" || x.Meal == "Ăn trưa" || x.Meal == "Lunch"),
            "snack" => query.Where(x => x.Meal == "Nhẹ" || x.Meal == "Ăn nhẹ" || x.Meal == "Snack"),
            _ => query
        };
        if (from is { } start) query = query.Where(x => x.EventDay >= start.Date);
        if (to is { } end) query = query.Where(x => x.EventDay < end.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(grade)) query = query.Where(x => x.Grade == grade.Trim());
        if (!string.IsNullOrWhiteSpace(className)) query = query.Where(x => x.ClassName == className.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100) return BadRequest("Search is too long.");
            query = query.Where(x => x.StudentName.Contains(term) || x.ErpStudentId.Contains(term));
        }

        switch (groupBy)
        {
            case "student": query = query.Where(x => x.StudentKey == key); break;
            case "grade": query = key == "—" ? query.Where(x => x.Grade == null) : query.Where(x => x.Grade == key); break;
            case "class": query = key == "—" ? query.Where(x => x.ClassName == null) : query.Where(x => x.ClassName == key); break;
            case "week":
            case "month":
                var prefix = groupBy + ":";
                if (!key.StartsWith(prefix, StringComparison.Ordinal) ||
                    !DateTime.TryParseExact(key[prefix.Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var period))
                    return BadRequest("Invalid report period key.");
                query = groupBy == "week"
                    ? query.Where(x => x.WeekStart == period)
                    : query.Where(x => x.MonthStart == period);
                break;
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var items = await query.OrderByDescending(x => x.EventDay)
            .ThenBy(x => x.StudentName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ErpMealReportDetailDto
            {
                Id = x.Id, StudentProfileId = x.StudentProfileId,
                ErpStudentId = x.ErpStudentId, StudentName = x.StudentName,
                Grade = x.Grade, ClassName = x.ClassName, EventDay = x.EventDay,
                Meal = x.Meal, OrderStatus = x.OrderStatus, TicketStatus = x.TicketStatus,
                ProductCode = x.ProductCode, ProductName = x.ProductName,
                Quantity = x.Quantity
            }).ToListAsync(cancellationToken);
        return Ok(new ErpMealReportDetailsPageDto
        {
            Items = items, Page = page, PageSize = pageSize,
            TotalCount = totalCount, TotalPages = totalPages
        });
    }

    private IQueryable<ErpMealDailyAggregate> ScopedRows(DateTime? from, DateTime? to)
    {
        var query = access.ApplyStudentScope(db.ErpMealDailyAggregates.AsNoTracking(), x => x.Student);
        if (from is { } start) query = query.Where(x => x.EventDay >= start.Date);
        if (to is { } end) query = query.Where(x => x.EventDay < end.Date.AddDays(1));
        return query;
    }

    private static bool ValidRange(DateTime? from, DateTime? to) =>
        from is null || to is null || from.Value.Date <= to.Value.Date &&
        to.Value.Date <= from.Value.Date.AddYears(5);

    private static string? NormalizeMealFilter(string? meal)
    {
        if (string.IsNullOrWhiteSpace(meal)) return null;
        var normalized = meal.Trim().ToLowerInvariant();
        return normalized is "breakfast" or "lunch" or "snack" ? normalized : "invalid";
    }
}
