using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using System.Text.Json;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
    private sealed record MetricSample(
        int StudentId, DateTime Date, double Value, Dictionary<string, string?> Fields);

    private sealed record CanvasMetricRow(
        int? StudentProfileId, string WorkflowState, bool Late, bool Missing, bool Excused,
        double? Score, string? Grade, double? PointsPossible, DateTime? SubmittedAt,
        DateTime? GradedAt, DateTime? DueAt, string? Subject, string? AssignmentTitle);

    private static Dictionary<string, string?> DetailFields(params (string Key, string? Value)[] fields) =>
        fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Key, field => field.Value);

    private static MetricSample CanvasSample(CanvasMetricRow row, DateTime date, double value) =>
        new(row.StudentProfileId!.Value, date, value,
            DetailFields(("Bài tập", row.AssignmentTitle), ("Môn học", row.Subject),
                ("Trạng thái", row.WorkflowState), ("Điểm", row.Score?.ToString("0.##")),
                ("Điểm tối đa", row.PointsPossible?.ToString("0.##")), ("Xếp loại", row.Grade),
                ("Hạn nộp", row.DueAt?.ToString("dd/MM/yyyy HH:mm"))));

    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<DashboardFilterOptionsDto>> FilterOptions()
    {
        var students = VisibleStudents();
        var periods = _context.AcademicPeriods.AsNoTracking();

        // Academic years / terms / subjects are sourced from the real data (Canvas warehouse,
        // academic periods and the imported student records) — not the legacy demo metrics.
        // "Measurement cycle" has no Canvas equivalent and is intentionally left empty.
        var canvasYears = _context.CanvasCourseScores.Where(s => s.AcademicYear != null).Select(s => s.AcademicYear!);
        var behaviourYears = _context.StudentBehaviours.Where(b => b.AcademicYear != null).Select(b => b.AcademicYear!);
        var projectYears = _context.StudentProjects.Where(p => p.AcademicYear != null).Select(p => p.AcademicYear!);
        var healthYears = _context.StudentMentalHealthRecords.Select(m => m.AcademicYear);
        var busYears = _context.StudentBusAttendanceRecords.Where(b => b.AcademicYear != null).Select(b => b.AcademicYear!);

        var result = new DashboardFilterOptionsDto
        {
            AcademicYears = await periods.Select(p => p.AcademicYear)
                .Concat(canvasYears).Concat(behaviourYears).Concat(projectYears).Concat(healthYears).Concat(busYears)
                .Distinct().OrderByDescending(value => value).ToListAsync(),
            Terms = await periods.Select(p => p.Term)
                .Concat(_context.CanvasCourseScores.Where(s => s.Term != null).Select(s => s.Term!))
                .Concat(_context.StudentBehaviours.Where(b => b.Term != null).Select(b => b.Term!))
                .Concat(_context.StudentProjects.Where(p => p.Term != null).Select(p => p.Term!))
                .Concat(_context.StudentBusAttendanceRecords.Where(b => b.Term != null).Select(b => b.Term!))
                .Distinct().OrderBy(value => value).ToListAsync(),
            Campuses = await students.Where(s => s.Campus != null).Select(s => s.Campus!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            EducationLevels = await students.Where(s => s.EducationLevel != null).Select(s => s.EducationLevel!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            GradeLevels = await students.Where(s => s.GradeLevel != null).Select(s => s.GradeLevel!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            Homerooms = await students.Where(s => s.Homeroom != null).Select(s => s.Homeroom!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            Students = await students.OrderBy(s => s.FullName).Select(s => new DashboardStudentOptionDto
            {
                Id = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
                GradeLevel = s.GradeLevel, Homeroom = s.Homeroom
            }).ToListAsync(),
            Programs = await students.Where(s => s.Program != null).Select(s => s.Program!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            Subjects = await _context.CanvasCourses.Where(c => c.Subject != null).Select(c => c.Subject!)
                .Concat(_context.StudentProjects.Where(p => p.Subject != null).Select(p => p.Subject!))
                .Concat(_context.StudentBehaviours.Where(b => b.Subject != null).Select(b => b.Subject!))
                .Distinct().OrderBy(value => value).ToListAsync()
        };
        return Ok(result);
    }

    [HttpGet("preference")]
    public async Task<ActionResult<DashboardPreferenceDto>> Preference()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var preference = await _context.DashboardPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId);
        return Ok(preference == null ? new DashboardPreferenceDto() : ToPreferenceDto(preference));
    }

    [HttpPut("preference")]
    public async Task<ActionResult<DashboardPreferenceDto>> SavePreference(DashboardPreferenceDto model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var preference = await _context.DashboardPreferences.FirstOrDefaultAsync(item => item.UserId == userId);
        if (preference == null)
        {
            preference = new DashboardPreference { UserId = userId };
            _context.DashboardPreferences.Add(preference);
        }
        preference.AcademicYear = Clean(model.AcademicYear);
        preference.Term = Clean(model.Term);
        preference.MeasurementCycle = Clean(model.MeasurementCycle);
        preference.Campus = Clean(model.Campus);
        preference.EducationLevel = Clean(model.EducationLevel);
        preference.GradeLevel = Clean(model.GradeLevel);
        preference.Homeroom = Clean(model.Homeroom);
        preference.StudentProfileId = model.StudentProfileId;
        preference.Program = Clean(model.Program);
        preference.Subject = Clean(model.Subject);
        preference.ChartOrdersJson = JsonSerializer.Serialize(model.ChartOrders ?? []);
        preference.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToPreferenceDto(preference));
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<List<DashboardMetricDto>>> Metrics(
        [FromQuery] string? year, [FromQuery] string? term, [FromQuery] string? cycle,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject)
    {
        var query = _context.DashboardMetricObservations.AsNoTracking()
            .Include(o => o.Metric).Where(o => o.Metric.IsActive);
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(o => o.StudentProfileId == null || visibleStudentIds.Contains(o.StudentProfileId.Value));
        }
        query = Apply(query, o => o.AcademicYear, year);
        query = Apply(query, o => o.Term, term);
        query = Apply(query, o => o.MeasurementCycle, cycle);
        query = Apply(query, o => o.Campus, campus);
        query = Apply(query, o => o.EducationLevel, level);
        query = Apply(query, o => o.GradeLevel, grade);
        query = Apply(query, o => o.Homeroom, homeroom);
        query = Apply(query, o => o.Program, program);
        query = Apply(query, o => o.Subject, subject);
        if (studentId.HasValue) query = query.Where(o => o.StudentProfileId == studentId);

        var rows = await query.OrderBy(o => o.PeriodStart).ToListAsync();
        var result = rows.GroupBy(o => new
        {
            o.Metric.Id,
            o.Metric.Key,
            o.Metric.Label,
            o.Metric.Domain,
            o.Metric.Source,
            o.Metric.Unit
        }).Select(group =>
        {
            var points = group.GroupBy(o => o.PeriodStart).Select(period => new DashboardMetricPointDto
            {
                PeriodStart = period.Key, Value = period.Sum(item => item.Value)
            }).OrderBy(point => point.PeriodStart).ToList();
            return new DashboardMetricDto
            {
                Id = group.Key.Key, Label = group.Key.Label, Category = group.Key.Domain,
                Source = group.Key.Source, Unit = group.Key.Unit,
                Value = points.LastOrDefault()?.Value,
                Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
                BaselineValue = group.OrderByDescending(o => o.PeriodStart).First().BaselineValue,
                KpiTarget = group.OrderByDescending(o => o.PeriodStart).First().KpiTarget,
                SupportsDrilldown = group.All(o => o.StudentProfileId != null),
                Series = points
            };
        }).ToList();

        result.AddRange(await ImportedDataMetrics(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject));
        result.AddRange(await CanvasDataMetrics(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject));
        result = result
            .OrderBy(metric => metric.Category)
            .ThenBy(metric => metric.Label)
            .ToList();
        return Ok(result);
    }

    /// <summary>
    /// Per-student breakdown of a single metric, so the workspace drill-down shows the
    /// students who actually drive the clicked metric (and their attributed value),
    /// rather than a generic activity ranking. Honours the same dashboard filters, plus
    /// the optional clicked month (<paramref name="periodStart"/>).
    /// </summary>
    [HttpGet("metric-drilldown")]
    public async Task<ActionResult<MetricDrilldownDto>> MetricDrilldown(
        [FromQuery] string metricId,
        [FromQuery] string? year, [FromQuery] string? term, [FromQuery] string? cycle,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject,
        [FromQuery] DateTime? periodStart,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(metricId))
        {
            return BadRequest("metricId is required.");
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Optional month window matching the clicked chart column.
        DateTime? monthStart = periodStart.HasValue
            ? new DateTime(periodStart.Value.Year, periodStart.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : null;
        var monthEnd = monthStart?.AddMonths(1);
        bool InMonth(DateTime date) => monthStart == null || (date >= monthStart && date < monthEnd);

        var hasYear = HasFilter(year);
        var hasTerm = HasFilter(term);
        (string Year, string Term, DateTime Start, DateTime End)? selectedPeriod = null;
        if (hasYear || hasTerm)
        {
            selectedPeriod = await ResolvePeriod(year, term, DateTime.UtcNow);
        }

        var samples = new List<MetricSample>();
        var aggregation = "count";
        var unit = "records";

        // ---- Behaviour, project and health metrics (imported warehouse data) ----
        if (metricId is "behaviour-records" or "concern-behaviour-records" or "commendation-records")
        {
            var query = FilterBehaviours(campus, level, grade, homeroom, program, subject, year, term)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (HasFilter(cycle)) query = query.Where(item => item.MeasurementCycle == cycle);
            if (hasYear && selectedPeriod.HasValue)
                query = query.Where(item => item.AcademicYear == year ||
                    (item.AcademicYear == null && item.IncidentDate >= selectedPeriod.Value.Start &&
                     item.IncidentDate < selectedPeriod.Value.End));
            if (hasTerm && selectedPeriod.HasValue)
                query = query.Where(item => item.Term == term ||
                    (item.Term == null && item.IncidentDate >= selectedPeriod.Value.Start &&
                     item.IncidentDate < selectedPeriod.Value.End));
            if (monthStart.HasValue)
                query = query.Where(item => item.IncidentDate >= monthStart.Value && item.IncidentDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.IncidentDate, item.BehaviourType,
                    item.Notes, item.RecordedByName, item.Subject })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId switch
                {
                    "concern-behaviour-records" =>
                        StudentClassificationService.IsConcerningBehaviourType(item.BehaviourType),
                    "commendation-records" =>
                        StudentClassificationService.IsCommendationBehaviourType(item.BehaviourType),
                    _ => true
                })
                .Select(item => new MetricSample(item.StudentProfileId, item.IncidentDate, 1.0,
                    DetailFields(("Loại hành vi", item.BehaviourType), ("Ghi chú", item.Notes),
                        ("Môn học", item.Subject), ("Ghi nhận bởi", item.RecordedByName)))));
        }
        else if (metricId is "project-records" or "active-project-records")
        {
            var query = FilterProjects(campus, level, grade, homeroom, program, subject, year, term)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (HasFilter(cycle)) query = query.Where(item => item.MeasurementCycle == cycle);
            if (hasYear && selectedPeriod.HasValue)
                query = query.Where(item => item.AcademicYear == year ||
                    (item.AcademicYear == null && (item.ActivityDate ?? item.CreatedAt) >= selectedPeriod.Value.Start &&
                     (item.ActivityDate ?? item.CreatedAt) < selectedPeriod.Value.End));
            if (hasTerm && selectedPeriod.HasValue)
                query = query.Where(item => item.Term == term ||
                    (item.Term == null && (item.ActivityDate ?? item.CreatedAt) >= selectedPeriod.Value.Start &&
                     (item.ActivityDate ?? item.CreatedAt) < selectedPeriod.Value.End));
            if (monthStart.HasValue)
                query = query.Where(item => (item.ActivityDate ?? item.CreatedAt) >= monthStart.Value &&
                    (item.ActivityDate ?? item.CreatedAt) < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.ActivityDate, item.CreatedAt, item.Status,
                    item.ProjectName, item.Subject, item.TeacherNotes })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId != "active-project-records" ||
                    !StudentDataEntryOptions.IsCompletedProjectStatus(item.Status))
                .Select(item => new MetricSample(item.StudentProfileId, item.ActivityDate ?? item.CreatedAt, 1.0,
                    DetailFields(("Dự án", item.ProjectName), ("Trạng thái", item.Status),
                        ("Môn học", item.Subject), ("Ghi chú", item.TeacherNotes)))));
        }
        else if (metricId is "mental-health-records" or "high-risk-mental-health")
        {
            // Mental health records carry no subject/cycle dimension.
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterMentalHealth(campus, level, grade, homeroom, program, null)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (hasYear) query = query.Where(item => item.AcademicYear == year);
            if (hasTerm && selectedPeriod.HasValue)
                query = query.Where(item => item.RecordDate >= selectedPeriod.Value.Start &&
                    item.RecordDate < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.RecordDate >= monthStart.Value && item.RecordDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.RecordDate, item.RiskLevel,
                    item.ConcernType, item.Notes, item.RecordedByName })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId != "high-risk-mental-health"
                    || string.Equals(item.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
                .Select(item => new MetricSample(item.StudentProfileId, item.RecordDate, 1.0,
                    DetailFields(("Loại vấn đề", item.ConcernType), ("Mức độ rủi ro", item.RiskLevel),
                        ("Ghi chú", item.Notes), ("Ghi nhận bởi", item.RecordedByName)))));
        }
        else if (metricId == "health-visit-records")
        {
            // Health clinic visits (from SIS) carry no subject/cycle/year dimension of their own.
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterSISHealthVisits(campus, level, grade, homeroom, program, null)
                .Where(item => item.StudentProfileId != null && (studentId == null || item.StudentProfileId == studentId));
            if ((hasYear || hasTerm) && selectedPeriod.HasValue)
                query = query.Where(item => item.ExamTime >= selectedPeriod.Value.Start &&
                    item.ExamTime < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.ExamTime >= monthStart.Value && item.ExamTime < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.ExamTime, item.Symptoms,
                    item.Diagnosis, item.Classification, item.Treatment, item.Medication, item.ExaminedBy })
                .ToListAsync();
            samples.AddRange(rows.Select(item => new MetricSample(item.StudentProfileId!.Value, item.ExamTime, 1.0,
                DetailFields(("Triệu chứng", item.Symptoms), ("Chẩn đoán", item.Diagnosis),
                    ("Phân loại", item.Classification), ("Xử trí", item.Treatment),
                    ("Thuốc", item.Medication), ("Khám bởi", item.ExaminedBy)))));
        }
        else if (metricId is "bus-late-records" or "bus-seatbelt-violations" or
                 "bus-late-to-school" or "bus-late-from-school")
        {
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterBusAttendance(campus, level, grade, homeroom, program, year, term)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (hasYear && selectedPeriod.HasValue)
                query = query.Where(item => item.AcademicYear == year ||
                    (item.AcademicYear == null && item.AttendanceDate >= selectedPeriod.Value.Start &&
                     item.AttendanceDate < selectedPeriod.Value.End));
            if (hasTerm && selectedPeriod.HasValue)
                query = query.Where(item => item.Term == term ||
                    (item.Term == null && item.AttendanceDate >= selectedPeriod.Value.Start &&
                     item.AttendanceDate < selectedPeriod.Value.End));
            if (monthStart.HasValue)
                query = query.Where(item => item.AttendanceDate >= monthStart.Value && item.AttendanceDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.AttendanceDate, item.ViolationType,
                    item.TripDirection, item.BusCode, item.RouteName, item.Notes, item.RecordedByName })
                .ToListAsync();
            samples.AddRange(rows.Where(item => metricId switch
                {
                    "bus-seatbelt-violations" => item.ViolationType == "No seatbelt",
                    "bus-late-to-school" => item.ViolationType == "Late" && item.TripDirection == "To school",
                    "bus-late-from-school" => item.ViolationType == "Late" && item.TripDirection == "From school",
                    _ => item.ViolationType == "Late"
                })
                .Select(item => new MetricSample(item.StudentProfileId, item.AttendanceDate, 1.0,
                    DetailFields(("Vi phạm", item.ViolationType), ("Chiều đi", item.TripDirection),
                        ("Mã xe", item.BusCode), ("Tuyến", item.RouteName), ("Ghi chú", item.Notes),
                        ("Ghi nhận bởi", item.RecordedByName)))));
        }
        // ---- Canvas assignment/submission metrics ----
        else if (metricId.StartsWith("canvas-assignment", StringComparison.Ordinal))
        {
            if (HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterCanvasSubmissions(campus, level, grade, homeroom, program, subject, year, term)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (monthStart.HasValue)
            {
                query = metricId switch
                {
                    "canvas-assignments-submitted" => query.Where(item => item.SubmittedAt >= monthStart.Value && item.SubmittedAt < monthEnd!.Value),
                    "canvas-assignments-missing" => query.Where(item => item.DueAt >= monthStart.Value && item.DueAt < monthEnd!.Value),
                    "canvas-assignments-late" => query.Where(item => item.SubmittedAt >= monthStart.Value && item.SubmittedAt < monthEnd!.Value),
                    _ => query.Where(item => (item.GradedAt ?? item.SubmittedAt) >= monthStart.Value &&
                        (item.GradedAt ?? item.SubmittedAt) < monthEnd!.Value)
                };
            }
            var rows = await query
                .Select(item => new CanvasMetricRow(
                    item.StudentProfileId, item.WorkflowState, item.Late, item.Missing, item.Excused,
                    item.Score, item.Grade, item.PointsPossible, item.SubmittedAt, item.GradedAt, item.DueAt,
                    item.Subject, item.Assignment != null ? item.Assignment.Title : null
                ))
                .ToListAsync();

            switch (metricId)
            {
                case "canvas-assignments-submitted":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r => r.SubmittedAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.SubmittedAt!.Value, 1.0)));
                    break;
                case "canvas-assignments-graded":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r =>
                            string.Equals(r.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase)
                            && r.StudentProfileId != null && (r.GradedAt != null || r.SubmittedAt != null))
                        .Select(r => CanvasSample(r, (r.GradedAt ?? r.SubmittedAt)!.Value, 1.0)));
                    break;
                case "canvas-assignments-missing":
                    unit = "assignments";
                    samples.AddRange(rows.Where(r => r.Missing && !r.Excused && r.DueAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.DueAt!.Value, 1.0)));
                    break;
                case "canvas-assignments-late":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r => r.Late && r.SubmittedAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.SubmittedAt!.Value, 1.0)));
                    break;
                case "canvas-assignment-score-avg":
                    aggregation = "average";
                    unit = "%";
                    samples.AddRange(rows.Where(r =>
                            string.Equals(r.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase)
                            && r.Score != null && r.PointsPossible is > 0 && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.GradedAt ?? r.SubmittedAt ?? DateTime.UtcNow,
                            r.Score!.Value / r.PointsPossible!.Value * 100)));
                    break;
                default:
                    return Ok(EmptyDrilldown(metricId));
            }
        }
        // ---- Canvas course grade ----
        else if (metricId == "canvas-course-grade-avg")
        {
            if (HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            aggregation = "average";
            unit = "%";
            var rows = await FilterCanvasScores(campus, level, grade, homeroom, program, subject, year, term)
                .Where(item => studentId == null || item.StudentProfileId == studentId)
                .Where(item => !monthStart.HasValue ||
                    ((item.UpdatedAt ?? item.CreatedAt) >= monthStart.Value &&
                     (item.UpdatedAt ?? item.CreatedAt) < monthEnd!.Value))
                .Select(item => new { item.StudentProfileId, item.FinalScore, item.CurrentScore,
                    item.CreatedAt, item.UpdatedAt, item.CourseName, item.Subject, item.GradingPeriodTitle })
                .ToListAsync();
            samples.AddRange(rows
                .Where(r => (r.FinalScore ?? r.CurrentScore) != null && r.StudentProfileId != null)
                .Select(r => new MetricSample(r.StudentProfileId!.Value, r.UpdatedAt ?? r.CreatedAt,
                    (r.FinalScore ?? r.CurrentScore)!.Value,
                    DetailFields(("Khóa học", r.CourseName), ("Môn học", r.Subject),
                        ("Kỳ chấm điểm", r.GradingPeriodTitle),
                        ("Điểm hiện tại", r.CurrentScore?.ToString("0.##")),
                        ("Điểm cuối kỳ", r.FinalScore?.ToString("0.##"))))));
        }
        // ---- Seeded observation-based metrics (SIS / Base ERP / warehouse) ----
        else
        {
            var query = _context.DashboardMetricObservations.AsNoTracking()
                .Include(o => o.Metric)
                .Where(o => o.Metric.Key == metricId && o.StudentProfileId != null);
            if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
                query = query.Where(o => o.StudentProfileId != null && visibleStudentIds.Contains(o.StudentProfileId.Value));
            }
            query = Apply(query, o => o.AcademicYear, year);
            query = Apply(query, o => o.Term, term);
            query = Apply(query, o => o.MeasurementCycle, cycle);
            query = Apply(query, o => o.Campus, campus);
            query = Apply(query, o => o.EducationLevel, level);
            query = Apply(query, o => o.GradeLevel, grade);
            query = Apply(query, o => o.Homeroom, homeroom);
            query = Apply(query, o => o.Program, program);
            query = Apply(query, o => o.Subject, subject);
            if (studentId.HasValue) query = query.Where(o => o.StudentProfileId == studentId);
            if (monthStart.HasValue)
                query = query.Where(o => o.PeriodStart >= monthStart.Value && o.PeriodStart < monthEnd!.Value);

            var metricRow = await _context.DashboardMetrics.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Key == metricId);
            unit = metricRow?.Unit ?? "";
            var rows = await query
                .Select(o => new { StudentId = o.StudentProfileId!.Value, o.PeriodStart, o.Value })
                .ToListAsync();
            samples.AddRange(rows.Select(r => new MetricSample(r.StudentId, r.PeriodStart,
                (double)r.Value, DetailFields(("Nguồn", "DashboardMetricObservation")))));
        }

        // Restrict to the clicked month, group per student, then attach student details.
        var scoped = samples.Where(sample => InMonth(sample.Date)).ToList();
        var perStudent = scoped
            .GroupBy(sample => sample.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Value = aggregation == "average"
                    ? Math.Round((decimal)group.Average(item => item.Value), 1)
                    : Math.Round((decimal)group.Sum(item => item.Value), 0)
            })
            .Where(row => row.Value > 0)
            .ToDictionary(row => row.StudentId, row => row.Value);

        var totalCount = perStudent.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var studentIds = perStudent
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => row.Key)
            .ToList();
        var rank = studentIds.Select((id, index) => new { id, index }).ToDictionary(item => item.id, item => item.index);
        var students = await _context.StudentProfiles.AsNoTracking()
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.StudentCode, student.FullName, student.GradeLevel, student.Homeroom })
            .ToListAsync();

        var recordsByStudent = scoped
            .Where(sample => studentIds.Contains(sample.StudentId))
            .GroupBy(sample => sample.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(sample => sample.Date)
                    .Take(10)
                    .Select(sample => new DrilldownRecordDto
                    {
                        Date = sample.Date,
                        Value = Math.Round((decimal)sample.Value, aggregation == "average" ? 1 : 0),
                        Fields = sample.Fields
                    })
                    .ToList());
        var recordCounts = scoped.GroupBy(sample => sample.StudentId)
            .ToDictionary(group => group.Key, group => group.Count());

        var result = new MetricDrilldownDto
        {
            MetricId = metricId,
            Label = LabelForMetric(metricId),
            Unit = unit,
            Aggregation = aggregation,
            AggregateValue = scoped.Count == 0
                ? null
                : aggregation == "average"
                    ? Math.Round((decimal)scoped.Average(sample => sample.Value), 1)
                    : Math.Round((decimal)scoped.Sum(sample => sample.Value), 0),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Students = students
                .Select(student => new MetricDrilldownRowDto
                {
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    FullName = student.FullName,
                    GradeLevel = student.GradeLevel,
                    Homeroom = student.Homeroom,
                    Value = perStudent[student.Id],
                    RecordCount = recordCounts[student.Id],
                    Records = recordsByStudent.GetValueOrDefault(student.Id, [])
                })
                .OrderBy(row => rank[row.StudentId])
                .ToList()
        };
        return Ok(result);
    }

    private MetricDrilldownDto EmptyDrilldown(string metricId) => new()
    {
        MetricId = metricId,
        Label = LabelForMetric(metricId),
        Aggregation = "count",
        Students = []
    };

    private static string LabelForMetric(string metricId) => metricId switch
    {
        "behaviour-records" => "Behaviour records",
        "concern-behaviour-records" => "Concerning behaviour records",
        "commendation-records" => "Commendation records",
        "project-records" => "Student projects",
        "active-project-records" => "Active student projects",
        "mental-health-records" => "Mental health records",
        "high-risk-mental-health" => "High-risk mental health records",
        "health-visit-records" => "Health office visits",
        "bus-late-records" => "Late bus check-ins",
        "bus-seatbelt-violations" => "Bus seatbelt violations",
        "bus-late-to-school" => "Late morning bus check-ins",
        "bus-late-from-school" => "Late afternoon bus check-ins",
        "canvas-course-grade-avg" => "Average course grade",
        "canvas-assignment-score-avg" => "Average assignment score",
        "canvas-assignments-submitted" => "Assignments submitted",
        "canvas-assignments-graded" => "Assignments graded",
        "canvas-assignments-missing" => "Missing assignments",
        "canvas-assignments-late" => "Late submissions",
        _ => metricId
    };

    private IQueryable<StudentBehaviour> FilterBehaviours(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, string? year, string? term)
    {
        var query = _context.StudentBehaviours.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program))
            query = query.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
        if (HasFilter(subject)) query = query.Where(item => item.Subject == subject);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == null || item.AcademicYear == year);
        if (HasFilter(term)) query = query.Where(item => item.Term == null || item.Term == term);
        return query;
    }

    private IQueryable<StudentProject> FilterProjects(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, string? year, string? term)
    {
        var query = _context.StudentProjects.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program))
            query = query.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
        if (HasFilter(subject)) query = query.Where(item => item.Subject == subject);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == null || item.AcademicYear == year);
        if (HasFilter(term)) query = query.Where(item => item.Term == null || item.Term == term);
        return query;
    }

    private IQueryable<StudentMentalHealthRecord> FilterMentalHealth(
        string? campus, string? level, string? grade, string? homeroom, string? program, string? year)
    {
        var query = _context.StudentMentalHealthRecords.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student.Program == program);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == year);
        return query;
    }

    private IQueryable<SISHealthVisit> FilterSISHealthVisits(
        string? campus, string? level, string? grade, string? homeroom, string? program, string? year)
    {
        var query = _context.SISHealthVisits.AsNoTracking().Where(item => item.StudentProfileId != null);
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student!.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        // Health visits store an exam timestamp; scope to the selected academic year (Aug–Jul).
        if (HasFilter(year) && TryAcademicYearRange(year!, out var start, out var end))
        {
            query = query.Where(item => item.ExamTime >= start && item.ExamTime < end);
        }
        return query;
    }

    private IQueryable<StudentBusAttendanceRecord> FilterBusAttendance(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? year, string? term)
    {
        var query = _context.StudentBusAttendanceRecords.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student.Program == program);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == null || item.AcademicYear == year);
        if (HasFilter(term)) query = query.Where(item => item.Term == null || item.Term == term);
        return query;
    }

    private static bool TryAcademicYearRange(string academicYear, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        var parts = academicYear.Split('-');
        if (parts.Length == 0 || !int.TryParse(parts[0], out var startYear))
        {
            return false;
        }
        start = new DateTime(startYear, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        end = new DateTime(startYear + 1, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }

    private IQueryable<CanvasSubmission> FilterCanvasSubmissions(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, string? year, string? term)
    {
        var query = _context.CanvasSubmissions.AsNoTracking().Where(item => item.StudentProfileId != null);
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student!.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        if (HasFilter(subject)) query = query.Where(item => item.Subject == subject);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == year);
        if (HasFilter(term)) query = query.Where(item => item.Term == term);
        return query;
    }

    private IQueryable<CanvasCourseScore> FilterCanvasScores(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, string? year, string? term)
    {
        var query = _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.CourseScore && item.StudentProfileId != null);
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            query = query.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
        }
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Student!.GradeLevel == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        if (HasFilter(subject)) query = query.Where(item => item.Subject == subject);
        if (HasFilter(year)) query = query.Where(item => item.AcademicYear == year);
        if (HasFilter(term)) query = query.Where(item => item.Term == term);
        return query;
    }

    private async Task<List<DashboardMetricDto>> ImportedDataMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject)
    {
        var behaviours = _context.StudentBehaviours.AsNoTracking().AsQueryable();
        var projects = _context.StudentProjects.AsNoTracking().AsQueryable();
        var mentalHealth = _context.StudentMentalHealthRecords.AsNoTracking().AsQueryable();
        var healthVisits = _context.SISHealthVisits.AsNoTracking().Where(item => item.StudentProfileId != null);
        var busRecords = _context.StudentBusAttendanceRecords.AsNoTracking().AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            behaviours = behaviours.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
            projects = projects.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
            mentalHealth = mentalHealth.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
            healthVisits = healthVisits.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
            busRecords = busRecords.Where(item => visibleStudentIds.Contains(item.StudentProfileId));
        }

        if (studentId.HasValue)
        {
            behaviours = behaviours.Where(item => item.StudentProfileId == studentId.Value);
            projects = projects.Where(item => item.StudentProfileId == studentId.Value);
            mentalHealth = mentalHealth.Where(item => item.StudentProfileId == studentId.Value);
            healthVisits = healthVisits.Where(item => item.StudentProfileId == studentId.Value);
            busRecords = busRecords.Where(item => item.StudentProfileId == studentId.Value);
        }
        if (HasFilter(campus))
        {
            behaviours = behaviours.Where(item => item.Student.Campus == campus);
            projects = projects.Where(item => item.Student.Campus == campus);
            mentalHealth = mentalHealth.Where(item => item.Student.Campus == campus);
            healthVisits = healthVisits.Where(item => item.Student!.Campus == campus);
            busRecords = busRecords.Where(item => item.Student.Campus == campus);
        }
        if (HasFilter(level))
        {
            behaviours = behaviours.Where(item => item.Student.EducationLevel == level);
            projects = projects.Where(item => item.Student.EducationLevel == level);
            mentalHealth = mentalHealth.Where(item => item.Student.EducationLevel == level);
            healthVisits = healthVisits.Where(item => item.Student!.EducationLevel == level);
            busRecords = busRecords.Where(item => item.Student.EducationLevel == level);
        }
        if (HasFilter(grade))
        {
            behaviours = behaviours.Where(item => item.Student.GradeLevel == grade);
            projects = projects.Where(item => item.Student.GradeLevel == grade);
            mentalHealth = mentalHealth.Where(item => item.Student.GradeLevel == grade);
            healthVisits = healthVisits.Where(item => item.Student!.GradeLevel == grade);
            busRecords = busRecords.Where(item => item.Student.GradeLevel == grade);
        }
        if (HasFilter(homeroom))
        {
            behaviours = behaviours.Where(item => item.Student.Homeroom == homeroom);
            projects = projects.Where(item => item.Student.Homeroom == homeroom);
            mentalHealth = mentalHealth.Where(item => item.Student.Homeroom == homeroom);
            healthVisits = healthVisits.Where(item => item.Student!.Homeroom == homeroom);
            busRecords = busRecords.Where(item => item.Student.Homeroom == homeroom);
        }
        if (HasFilter(program))
        {
            behaviours = behaviours.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
            projects = projects.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
            mentalHealth = mentalHealth.Where(item => item.Student.Program == program);
            healthVisits = healthVisits.Where(item => item.Student!.Program == program);
            busRecords = busRecords.Where(item => item.Student.Program == program);
        }
        if (HasFilter(subject))
        {
            behaviours = behaviours.Where(item => item.Subject == subject);
            projects = projects.Where(item => item.Subject == subject);
            // Mental health and clinic visits have no subject dimension.
            mentalHealth = mentalHealth.Where(_ => false);
            healthVisits = healthVisits.Where(_ => false);
            busRecords = busRecords.Where(_ => false);
        }
        if (HasFilter(cycle))
        {
            behaviours = behaviours.Where(item => item.MeasurementCycle == cycle);
            projects = projects.Where(item => item.MeasurementCycle == cycle);
            // These are event records and do not use a measurement cycle.
            mentalHealth = mentalHealth.Where(_ => false);
            healthVisits = healthVisits.Where(_ => false);
            busRecords = busRecords.Where(_ => false);
        }

        var hasYear = HasFilter(year);
        var hasTerm = HasFilter(term);
        (string Year, string Term, DateTime Start, DateTime End)? period = null;
        if (hasYear || hasTerm)
        {
            period = await ResolvePeriod(year, term, DateTime.UtcNow);
        }
        if (hasYear && period.HasValue)
        {
            var selectedYear = year!;
            var start = period.Value.Start;
            var end = period.Value.End;
            behaviours = behaviours.Where(item => item.AcademicYear == selectedYear ||
                (item.AcademicYear == null && item.IncidentDate >= start && item.IncidentDate < end));
            projects = projects.Where(item => item.AcademicYear == selectedYear ||
                (item.AcademicYear == null && (item.ActivityDate ?? item.CreatedAt) >= start &&
                 (item.ActivityDate ?? item.CreatedAt) < end));
            mentalHealth = mentalHealth.Where(item => item.AcademicYear == selectedYear);
            healthVisits = healthVisits.Where(item => item.ExamTime >= start && item.ExamTime < end);
            busRecords = busRecords.Where(item => item.AcademicYear == selectedYear ||
                (item.AcademicYear == null && item.AttendanceDate >= start && item.AttendanceDate < end));
        }
        if (hasTerm && period.HasValue)
        {
            var selectedTerm = term!;
            var start = period.Value.Start;
            var end = period.Value.End;
            behaviours = behaviours.Where(item => item.Term == selectedTerm ||
                (item.Term == null && item.IncidentDate >= start && item.IncidentDate < end));
            projects = projects.Where(item => item.Term == selectedTerm ||
                (item.Term == null && (item.ActivityDate ?? item.CreatedAt) >= start &&
                 (item.ActivityDate ?? item.CreatedAt) < end));
            mentalHealth = mentalHealth.Where(item => item.RecordDate >= start && item.RecordDate < end);
            healthVisits = healthVisits.Where(item => item.ExamTime >= start && item.ExamTime < end);
            busRecords = busRecords.Where(item => item.Term == selectedTerm ||
                (item.Term == null && item.AttendanceDate >= start && item.AttendanceDate < end));
        }

        var behaviourRows = await behaviours
            .Select(item => new { Date = item.IncidentDate, Type = item.BehaviourType })
            .ToListAsync();
        var projectRows = await projects
            .Select(item => new { Date = item.ActivityDate ?? item.CreatedAt, item.Status })
            .ToListAsync();
        var mentalHealthRows = await mentalHealth
            .Select(item => new { Date = item.RecordDate, item.RiskLevel })
            .ToListAsync();
        var healthVisitRows = await healthVisits
            .Select(item => new { Date = item.ExamTime })
            .ToListAsync();
        var busRows = await busRecords
            .Select(item => new { Date = item.AttendanceDate, item.ViolationType, item.TripDirection })
            .ToListAsync();

        return
        [
            CountMetric("behaviour-records", "Behaviour records", "Compliance", "StudentBehaviours",
                behaviourRows.Select(item => item.Date)),
            CountMetric("concern-behaviour-records", "Concerning behaviour records", "Compliance", "StudentBehaviours",
                behaviourRows.Where(item => StudentClassificationService.IsConcerningBehaviourType(item.Type))
                    .Select(item => item.Date)),
            CountMetric("commendation-records", "Commendation records", "Achievement", "StudentBehaviours",
                behaviourRows.Where(item => StudentClassificationService.IsCommendationBehaviourType(item.Type))
                    .Select(item => item.Date)),
            CountMetric("project-records", "Student projects", "Achievement", "StudentProjects",
                projectRows.Select(item => item.Date)),
            CountMetric("active-project-records", "Active student projects", "Achievement", "StudentProjects",
                projectRows.Where(item => !StudentDataEntryOptions.IsCompletedProjectStatus(item.Status))
                    .Select(item => item.Date)),
            CountMetric("mental-health-records", "Mental health records", "Safety", "StudentMentalHealth",
                mentalHealthRows.Select(item => item.Date)),
            CountMetric("high-risk-mental-health", "High-risk mental health records", "Safety", "StudentMentalHealth",
                mentalHealthRows.Where(item => string.Equals(item.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Date)),
            CountMetric("health-visit-records", "Health office visits", "Safety", "SISHealthVisits",
                healthVisitRows.Select(item => item.Date)),
            CountMetric("bus-late-records", "Late bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late").Select(item => item.Date)),
            CountMetric("bus-seatbelt-violations", "Bus seatbelt violations", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "No seatbelt").Select(item => item.Date)),
            CountMetric("bus-late-to-school", "Late morning bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late" && item.TripDirection == "To school")
                    .Select(item => item.Date)),
            CountMetric("bus-late-from-school", "Late afternoon bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late" && item.TripDirection == "From school")
                    .Select(item => item.Date))
        ];
    }

    /// <summary>
    /// Metrics derived live from the Canvas warehouse tables (grades and assignments).
    /// These replace the retired Canvas "assumption" demo metrics. They honour the same
    /// visibility and dashboard filters as the other imported sources.
    /// </summary>
    private async Task<List<DashboardMetricDto>> CanvasDataMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject)
    {
        // Canvas records carry no measurement cycle, so a cycle-scoped report has no Canvas rows.
        if (HasFilter(cycle))
        {
            return [];
        }

        var submissions = _context.CanvasSubmissions.AsNoTracking()
            .Where(item => item.StudentProfileId != null).AsQueryable();
        var scores = _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.CourseScore && item.StudentProfileId != null).AsQueryable();

        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visibleStudentIds = SISClassVisibility.StudentIds(_context, userId);
            submissions = submissions.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
            scores = scores.Where(item => item.StudentProfileId != null && visibleStudentIds.Contains(item.StudentProfileId.Value));
        }
        if (studentId.HasValue)
        {
            submissions = submissions.Where(item => item.StudentProfileId == studentId.Value);
            scores = scores.Where(item => item.StudentProfileId == studentId.Value);
        }
        if (HasFilter(campus))
        {
            submissions = submissions.Where(item => item.Student!.Campus == campus);
            scores = scores.Where(item => item.Student!.Campus == campus);
        }
        if (HasFilter(level))
        {
            submissions = submissions.Where(item => item.Student!.EducationLevel == level);
            scores = scores.Where(item => item.Student!.EducationLevel == level);
        }
        if (HasFilter(grade))
        {
            submissions = submissions.Where(item => item.Student!.GradeLevel == grade);
            scores = scores.Where(item => item.Student!.GradeLevel == grade);
        }
        if (HasFilter(homeroom))
        {
            submissions = submissions.Where(item => item.Student!.Homeroom == homeroom);
            scores = scores.Where(item => item.Student!.Homeroom == homeroom);
        }
        if (HasFilter(program))
        {
            submissions = submissions.Where(item => item.Student!.Program == program);
            scores = scores.Where(item => item.Student!.Program == program);
        }
        if (HasFilter(subject))
        {
            submissions = submissions.Where(item => item.Subject == subject);
            scores = scores.Where(item => item.Subject == subject);
        }
        if (HasFilter(year))
        {
            submissions = submissions.Where(item => item.AcademicYear == year);
            scores = scores.Where(item => item.AcademicYear == year);
        }
        if (HasFilter(term))
        {
            submissions = submissions.Where(item => item.Term == term);
            scores = scores.Where(item => item.Term == term);
        }

        var submissionRows = await submissions.Select(item => new
        {
            item.WorkflowState, item.Late, item.Missing, item.Excused,
            item.Score, item.PointsPossible,
            item.SubmittedAt, item.GradedAt, item.DueAt
        }).ToListAsync();
        var scoreRows = await scores.Select(item => new
        {
            item.FinalScore, item.CurrentScore, item.CreatedAt, item.UpdatedAt
        }).ToListAsync();

        // Use each submission's most meaningful timeline date for the monthly series.
        var gradedSubs = submissionRows
            .Where(item => string.Equals(item.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var courseGradePoints = scoreRows
            .Where(item => (item.FinalScore ?? item.CurrentScore) != null)
            .Select(item => (Date: item.UpdatedAt ?? item.CreatedAt, Value: (item.FinalScore ?? item.CurrentScore)!.Value));

        var assignmentScorePoints = gradedSubs
            .Where(item => item.Score != null && item.PointsPossible is > 0)
            .Select(item => (
                Date: item.GradedAt ?? item.SubmittedAt ?? DateTime.UtcNow,
                Value: item.Score!.Value / item.PointsPossible!.Value * 100));

        return
        [
            AverageMetric("canvas-course-grade-avg", "Average course grade", "Academics", "Canvas", "%",
                courseGradePoints),
            AverageMetric("canvas-assignment-score-avg", "Average assignment score", "Academics", "Canvas", "%",
                assignmentScorePoints),
            CountMetric("canvas-assignments-submitted", "Assignments submitted", "Academics", "Canvas",
                submissionRows.Where(item => item.SubmittedAt != null).Select(item => item.SubmittedAt!.Value), "submissions"),
            CountMetric("canvas-assignments-graded", "Assignments graded", "Academics", "Canvas",
                gradedSubs.Where(item => item.GradedAt != null || item.SubmittedAt != null)
                    .Select(item => (item.GradedAt ?? item.SubmittedAt)!.Value), "submissions"),
            CountMetric("canvas-assignments-missing", "Missing assignments", "Academics", "Canvas",
                submissionRows.Where(item => item.Missing && !item.Excused && item.DueAt != null)
                    .Select(item => item.DueAt!.Value), "assignments"),
            CountMetric("canvas-assignments-late", "Late submissions", "Academics", "Canvas",
                submissionRows.Where(item => item.Late && item.SubmittedAt != null)
                    .Select(item => item.SubmittedAt!.Value), "submissions")
        ];
    }

    private static DashboardMetricDto AverageMetric(
        string id, string label, string category, string source, string unit,
        IEnumerable<(DateTime Date, double Value)> samples)
    {
        var values = samples.ToList();
        var points = values
            .GroupBy(sample => new DateTime(sample.Date.Year, sample.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(group => group.Key)
            .Select(group => new DashboardMetricPointDto
            {
                PeriodStart = group.Key,
                Value = Math.Round((decimal)group.Average(sample => sample.Value), 1)
            })
            .ToList();

        return new DashboardMetricDto
        {
            Id = id,
            Label = label,
            Category = category,
            Source = source,
            Unit = unit,
            Value = values.Count == 0 ? null : Math.Round((decimal)values.Average(sample => sample.Value), 1),
            Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
            Series = points
        };
    }

    private static DashboardMetricDto CountMetric(
        string id, string label, string category, string source, IEnumerable<DateTime> dates,
        string unit = "records")
    {
        var values = dates.ToList();
        var points = values
            .GroupBy(date => new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(group => group.Key)
            .Select(group => new DashboardMetricPointDto
            {
                PeriodStart = group.Key,
                Value = group.Count()
            })
            .ToList();

        return new DashboardMetricDto
        {
            Id = id,
            Label = label,
            Category = category,
            Source = source,
            Unit = unit,
            Value = values.Count,
            Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
            Series = points
        };
    }

    private static bool HasFilter(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(
        [FromQuery] string? year,
        [FromQuery] string? term)
    {
        var now = DateTime.UtcNow;
        var period = await ResolvePeriod(year, term, now);

        var students = await VisibleStudents()
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .AsSplitQuery()
            .ToListAsync();

        var overview = new DashboardOverviewDto
        {
            PeriodYear = period.Year,
            PeriodTerm = period.Term,
            TotalStudents = students.Count,
            TotalProjects = students.Sum(student => student.Projects.Count(project => InPeriod(project, period))),
            ActiveProjects = students.Sum(student => student.Projects
                .Count(project => InPeriod(project, period) && !StudentDataEntryOptions.IsCompletedProjectStatus(project.Status)))
        };

        var since = now.AddDays(-30);
        var gradeBuckets = new Dictionary<string, DashboardGradeDto>();
        var attentionCandidates = new List<(StudentProfile Student, string RiskLevel, int ConcernCount)>();

        foreach (var student in students)
        {
            var periodBehaviours = student.Behaviours.Where(behaviour => InPeriod(behaviour, period)).ToList();
            var concernCount = periodBehaviours.Count(StudentClassificationService.IsConcerningBehaviour);
            var risk = StudentClassificationService.ClassifyRisk(student.GradeLevel, concernCount);
            switch (risk.Level)
            {
                case "High":
                    overview.AtRiskHigh++;
                    break;
                case "Medium":
                    overview.AtRiskMedium++;
                    break;
                default:
                    overview.OnTrack++;
                    break;
            }

            if (risk.Level != "None")
            {
                attentionCandidates.Add((student, risk.Level, concernCount));
            }

            foreach (var behaviour in student.Behaviours)
            {
                // Behaviour activity is scoped to the selected reporting period.
                if (InPeriod(behaviour, period))
                {
                    overview.TotalBehaviours++;
                }

                if (behaviour.IncidentDate >= since)
                {
                    overview.BehavioursLast30Days++;
                }
            }

            var gradeKey = string.IsNullOrWhiteSpace(student.GradeLevel) ? "—" : student.GradeLevel!;
            if (!gradeBuckets.TryGetValue(gradeKey, out var bucket))
            {
                bucket = new DashboardGradeDto { Grade = gradeKey };
                gradeBuckets[gradeKey] = bucket;
            }

            bucket.Students++;
            if (risk.Level != "None")
            {
                bucket.AtRisk++;
            }
        }

        overview.AtRiskTotal = overview.AtRiskHigh + overview.AtRiskMedium;
        overview.BehaviourTrend = BuildTrend(students, period.Start, period.End, now);
        overview.GradeDistribution = gradeBuckets.Values
            .OrderBy(grade => grade.Grade, StringComparer.OrdinalIgnoreCase)
            .ToList();
        overview.TopAtRisk = attentionCandidates
            .OrderBy(candidate => candidate.RiskLevel == "High" ? 0 : 1)
            .ThenByDescending(candidate => candidate.ConcernCount)
            .ThenBy(candidate => candidate.Student.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(candidate => new DashboardAttentionStudentDto
            {
                Id = candidate.Student.Id,
                StudentCode = candidate.Student.StudentCode,
                FullName = candidate.Student.FullName,
                GradeLevel = candidate.Student.GradeLevel,
                Homeroom = candidate.Student.Homeroom,
                RiskLevel = candidate.RiskLevel
            })
            .ToList();

        return Ok(overview);
    }

    private async Task<(string Year, string Term, DateTime Start, DateTime End)> ResolvePeriod(
        string? year, string? term, DateTime now)
    {
        var storedPeriods = _context.AcademicPeriods.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(year)) storedPeriods = storedPeriods.Where(p => p.AcademicYear == year);
        if (!string.IsNullOrWhiteSpace(term) && term != "all") storedPeriods = storedPeriods.Where(p => p.Term == term);
        var configured = await storedPeriods.OrderByDescending(p => p.IsCurrent).ThenBy(p => p.StartDate).ToListAsync();
        if (configured.Count > 0)
        {
            return (configured[0].AcademicYear, string.IsNullOrWhiteSpace(term) ? "all" : term,
                configured.Min(p => p.StartDate), configured.Max(p => p.EndDate));
        }

        // School year runs August–July. Default to the year that contains "now".
        var startYear = now.Month >= 8 ? now.Year : now.Year - 1;
        if (!string.IsNullOrWhiteSpace(year))
        {
            var parts = year.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[0], out var parsed))
            {
                startYear = parsed;
            }
        }

        var normalizedTerm = term switch
        {
            "hk1" => "hk1",
            "hk2" => "hk2",
            _ => "all"
        };

        var (start, end) = normalizedTerm switch
        {
            "hk1" => (new DateTime(startYear, 8, 1), new DateTime(startYear + 1, 1, 1)),
            "hk2" => (new DateTime(startYear + 1, 1, 1), new DateTime(startYear + 1, 7, 1)),
            _ => (new DateTime(startYear, 8, 1), new DateTime(startYear + 1, 7, 1))
        };

        return ($"{startYear}-{startYear + 1}", normalizedTerm, start, end);
    }

    private static bool InPeriod(StudentBehaviour item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return item.AcademicYear == period.Year && (period.Term == "all" || item.Term == period.Term);
        return item.IncidentDate >= period.Start && item.IncidentDate < period.End;
    }

    private static bool InPeriod(StudentProject item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return item.AcademicYear == period.Year && (period.Term == "all" || item.Term == period.Term);
        var date = item.ActivityDate ?? item.CreatedAt;
        return date >= period.Start && date < period.End;
    }

    private static List<DashboardTrendPointDto> BuildTrend(
        List<StudentProfile> students, DateTime start, DateTime end, DateTime now)
    {
        var points = new List<DashboardTrendPointDto>();
        var index = new Dictionary<(int Year, int Month), DashboardTrendPointDto>();

        // Show elapsed months of the period only (never future months).
        var cursor = new DateTime(start.Year, start.Month, 1);
        var limit = end < now ? end : now;
        while (cursor <= limit && points.Count < 12)
        {
            var point = new DashboardTrendPointDto { Year = cursor.Year, Month = cursor.Month };
            points.Add(point);
            index[(cursor.Year, cursor.Month)] = point;
            cursor = cursor.AddMonths(1);
        }

        foreach (var behaviour in students.SelectMany(student => student.Behaviours))
        {
            if (index.TryGetValue((behaviour.IncidentDate.Year, behaviour.IncidentDate.Month), out var point))
            {
                point.Total++;
            }
        }

        return points;
    }

    private IQueryable<StudentProfile> VisibleStudents()
    {
        var query = _context.StudentProfiles.AsNoTracking();

        if (User.IsInRole("Admin") || User.IsInRole("Principal"))
        {
            return query;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return SISClassVisibility.Students(_context, userId);
    }

    private static IQueryable<DashboardMetricObservation> Apply(
        IQueryable<DashboardMetricObservation> query,
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "all") return query;
        return query.Where(BuildEquality(selector, value));
    }

    private static System.Linq.Expressions.Expression<Func<DashboardMetricObservation, bool>> BuildEquality(
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector, string value)
    {
        var equals = System.Linq.Expressions.Expression.Equal(selector.Body,
            System.Linq.Expressions.Expression.Constant(value, typeof(string)));
        return System.Linq.Expressions.Expression.Lambda<Func<DashboardMetricObservation, bool>>(equals, selector.Parameters);
    }

    private static DashboardPreferenceDto ToPreferenceDto(DashboardPreference item) => new()
    {
        AcademicYear = item.AcademicYear, Term = item.Term, MeasurementCycle = item.MeasurementCycle,
        Campus = item.Campus, EducationLevel = item.EducationLevel, GradeLevel = item.GradeLevel,
        Homeroom = item.Homeroom, StudentProfileId = item.StudentProfileId,
        Program = item.Program, Subject = item.Subject,
        ChartOrders = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(item.ChartOrdersJson) ?? []
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
