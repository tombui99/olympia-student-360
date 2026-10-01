using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

/// <summary>
/// Ad-hoc "data explorer" over the raw Canvas warehouse tables. Unlike the simplified
/// dashboard metrics, this lets the workspace build charts by picking a dataset, a
/// dimension to group by, and a measure to aggregate — the field values come straight
/// from the Canvas data. Datasets/dimensions/measures are whitelisted so the request
/// can never inject arbitrary columns.
/// </summary>
[Route("api/dashboard/canvas-explore")]
[ApiController]
[Authorize]
public class CanvasExploreController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly UserAccessService _userAccess;

    public CanvasExploreController(AppDbContext context, IMemoryCache cache, UserAccessService userAccess)
    {
        _context = context;
        _cache = cache;
        _userAccess = userAccess;
    }

    [HttpGet("options")]
    public ActionResult<CanvasExploreOptionsDto> Options() => Ok(BuildOptions());

    [HttpGet]
    public async Task<ActionResult<CanvasExploreResultDto>> Explore(
        [FromQuery] string dataset, [FromQuery] string dimension, [FromQuery] string measure,
        [FromQuery] string? year, [FromQuery] string? term,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject)
    {
        var options = BuildOptions();
        var datasetOption = options.Datasets.FirstOrDefault(d => d.Key == dataset);
        if (datasetOption == null)
        {
            return BadRequest($"Unknown dataset '{dataset}'.");
        }
        if (datasetOption.Dimensions.All(d => d.Key != dimension))
        {
            return BadRequest($"Unknown dimension '{dimension}' for dataset '{dataset}'.");
        }
        var measureOption = datasetOption.Measures.FirstOrDefault(m => m.Key == measure);
        if (measureOption == null)
        {
            return BadRequest($"Unknown measure '{measure}' for dataset '{dataset}'.");
        }

        var cacheKey = ExploreCacheKey(dataset, dimension, measure, year, term, campus, level,
            grade, homeroom, studentId?.ToString(), program, subject, _userAccess.Current.CacheKey);
        if (_cache.TryGetValue<CanvasExploreResultDto>(cacheKey, out var cached) && cached is not null)
        {
            return Ok(cached);
        }

        var f = await BuildFilters(year, term, campus, level, grade, homeroom, studentId, program, subject);
        var rows = dataset switch
        {
            "submissions" => await SubmissionRows(f, dimension, measure),
            "scores" => await ScoreRows(f, dimension, measure),
            "assignments" => await AssignmentRows(f, dimension, measure),
            "courses" => await CourseRows(f, dimension, measure),
            "parents" => await ParentRows(f, dimension, measure),
            _ => []
        };

        var aggregation = Aggregation(measure);
        var grouped = rows
            .Where(row => row.Value.HasValue)
            .GroupBy(row => row.Key)
            .Select(group => new CanvasExploreRowDto
            {
                Key = group.Key,
                Value = aggregation switch
                {
                    "average" => Math.Round((decimal)group.Average(item => item.Value!.Value), 1),
                    _ => Math.Round((decimal)group.Sum(item => item.Value!.Value), aggregation == "sum" ? 1 : 0)
                }
            })
            .ToList();

        // Month dimensions read best chronologically; everything else ranks by value.
        grouped = dimension == "month"
            ? grouped.OrderBy(row => row.Key).ToList()
            : grouped.OrderByDescending(row => row.Value).ThenBy(row => row.Key).ToList();

        var result = new CanvasExploreResultDto
        {
            Dataset = dataset,
            Dimension = dimension,
            Measure = measure,
            Unit = measureOption.Unit,
            Aggregation = aggregation,
            Rows = grouped
        };
        _cache.Set(cacheKey, result, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
        return Ok(result);
    }

    [HttpGet("drilldown")]
    public async Task<ActionResult<CanvasExploreDrilldownDto>> Drilldown(
        [FromQuery] string dataset, [FromQuery] string dimension, [FromQuery] string measure,
        [FromQuery] string groupKey,
        [FromQuery] string? year, [FromQuery] string? term,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var datasetOption = BuildOptions().Datasets.FirstOrDefault(item => item.Key == dataset);
        if (datasetOption == null || datasetOption.Dimensions.All(item => item.Key != dimension) ||
            datasetOption.Measures.All(item => item.Key != measure))
        {
            return BadRequest("Unknown explorer dataset, dimension or measure.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var filters = await BuildFilters(year, term, campus, level, grade, homeroom, studentId, program, subject);
        var records = dataset switch
        {
            "submissions" => await SubmissionDrilldownRows(filters, dimension, measure, groupKey),
            "scores" => await ScoreDrilldownRows(filters, dimension, measure, groupKey),
            "assignments" => await AssignmentDrilldownRows(filters, dimension, measure, groupKey),
            "courses" => await CourseDrilldownRows(filters, dimension, measure, groupKey),
            "parents" => await ParentDrilldownRows(filters, dimension, groupKey),
            _ => []
        };

        var totalCount = records.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        return Ok(new CanvasExploreDrilldownDto
        {
            Dataset = dataset,
            GroupKey = groupKey,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Records = records.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        });
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> SubmissionDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = _context.CanvasSubmissions.AsNoTracking().Where(item => item.StudentProfileId != null);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(item => item.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item =>
            (item.Assignment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
             item.Student.CanvasGradeLevel) == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(item => item.Assignment!.Course!.Name != null &&
            item.Assignment.Course.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.Period != null)
            query = query.Where(item =>
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) >= f.Period.Start &&
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) < f.Period.End);

        var rows = await query.Select(item => new
        {
            item.StudentProfileId, item.Student!.SISUserId, item.Student.FullName,
            GradeLevel = item.Assignment!.Course!.CanvasGradeLevel ?? item.Student.GradeLevel ?? item.Student.CanvasGradeLevel,
            item.Student.Homeroom,
            Subject = item.Assignment!.Course!.Name, Term = item.Assignment.Course.EnrollmentTermName,
            item.WorkflowState, item.Late, item.Missing, item.Excused, item.Score,
            PointsPossible = item.Assignment.PointsPossible, item.SubmittedAt,
            DueAt = item.CachedDueDate ?? item.Assignment.DueAt,
            AssignmentTitle = item.Assignment.Name
        }).ToListAsync();

        string Dim(dynamic item) => dimension switch
        {
            "subject" => Text(item.Subject), "term" => TermLabel(item.Term),
            "workflowState" => Text(item.WorkflowState), "gradeLevel" => Text(item.GradeLevel),
            "homeroom" => Text(item.Homeroom), "month" => Month(item.DueAt),
            "status" => SubmissionStatus(item.Missing, item.Excused, item.Late, item.SubmittedAt),
            _ => "—"
        };
        decimal? Value(dynamic item) => measure switch
        {
            "count" => 1m,
            "avgScorePercent" when IsGraded(item.WorkflowState) && item.Score != null && item.PointsPossible > 0
                => (decimal)(item.Score / item.PointsPossible * 100),
            "avgScore" when IsGraded(item.WorkflowState) => (decimal?)item.Score,
            "sumPoints" => (decimal?)item.PointsPossible,
            _ => null
        };
        return rows.Where(item => Dim(item) == groupKey && Value(item) != null)
            .OrderByDescending(item => item.SubmittedAt ?? item.DueAt)
            .Select(item => ExplorerRecord(item.StudentProfileId, item.SISUserId, item.FullName,
                item.GradeLevel, item.Homeroom, item.SubmittedAt ?? item.DueAt, Value(item),
                ("Bài tập", item.AssignmentTitle), ("Môn học", item.Subject),
                ("Trạng thái", SubmissionStatus(item.Missing, item.Excused, item.Late, item.SubmittedAt)),
                ("Điểm", item.Score?.ToString("0.##")), ("Điểm tối đa", item.PointsPossible?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> ScoreDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = _context.CanvasCourseScores.AsNoTracking().Where(item => item.StudentProfileId != null);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(item => item.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item =>
            (item.Enrollment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
             item.Student.CanvasGradeLevel) == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(item => item.Enrollment!.Course!.Name != null &&
            item.Enrollment.Course.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.TermAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.GradingPeriodTitle, f.TermAliases);
        var rows = await query.Select(item => new
        {
            item.StudentProfileId, item.Student!.SISUserId, item.Student.FullName,
            GradeLevel = item.Enrollment!.Course!.CanvasGradeLevel ?? item.Student.GradeLevel ?? item.Student.CanvasGradeLevel,
            item.Student.Homeroom,
            CourseName = item.Enrollment!.Course!.Name,
            EnrollmentTermName = item.Enrollment.Course.EnrollmentTermName,
            item.GradingPeriodTitle,
            item.FinalScore, item.CurrentScore,
            Date = item.GradingPeriodEndDate
        }).ToListAsync();
        string Dim(dynamic item) => dimension switch
        {
            "subject" => Text(item.CourseName), "term" => Text(item.EnrollmentTermName),
            "gradingPeriod" => Text(item.GradingPeriodTitle), "courseName" => Text(item.CourseName),
            "gradeLevel" => Text(item.GradeLevel), "homeroom" => Text(item.Homeroom), _ => "—"
        };
        decimal? Value(dynamic item) => measure switch
        {
            "count" => 1m, "avgFinalScore" => (decimal?)item.FinalScore,
            "avgCurrentScore" => (decimal?)item.CurrentScore, _ => null
        };
        return rows.Where(item => Dim(item) == groupKey && Value(item) != null)
            .OrderByDescending(item => item.Date)
            .Select(item => ExplorerRecord(item.StudentProfileId, item.SISUserId, item.FullName,
                item.GradeLevel, item.Homeroom, item.Date, Value(item),
                ("Canvas course name", item.CourseName),
                ("Canvas enrollment term name", item.EnrollmentTermName),
                ("Canvas grading_period.title", item.GradingPeriodTitle),
                ("Canvas grades.current_score", item.CurrentScore?.ToString("0.##")),
                ("Canvas grades.final_score", item.FinalScore?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> AssignmentDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var assignments = ApplyStudentScope(_context.CanvasAssignments.AsNoTracking(), f);
        if (f.Has(f.Subject)) assignments = assignments.Where(item => item.Course!.Name != null &&
            item.Course.Name.Trim() == f.Subject);
        assignments = ApplyAcademicYear(assignments, f.YearAliases);
        if (f.Period != null)
            assignments = assignments.Where(item => item.DueAt >= f.Period.Start && item.DueAt < f.Period.End);
        var rows = await assignments.Select(assignment => new
        {
            assignment.Name, assignment.AssignmentGroupName, assignment.WorkflowState,
            assignment.DueAt, assignment.PointsPossible,
            Subject = assignment.Course!.Name,
            AcademicYear = assignment.Course.EnrollmentTermName
        }).ToListAsync();
        string Dim(dynamic item) => dimension switch
        {
            "subject" => Text(item.Subject), "assignmentGroup" => Text(item.AssignmentGroupName),
            "workflowState" => Text(item.WorkflowState), "month" => Month(item.DueAt), _ => "—"
        };
        decimal? Value(dynamic item) => measure == "count" ? 1m : (decimal?)item.PointsPossible;
        return rows.Where(item => Dim(item) == groupKey && Value(item) != null)
            .OrderByDescending(item => item.DueAt)
            .Select(item => ExplorerRecord(null, null, null, null, null, item.DueAt, Value(item),
                ("Bài tập", item.Name), ("Môn học", item.Subject),
                ("Nhóm bài tập", item.AssignmentGroupName), ("Trạng thái", item.WorkflowState),
                ("Điểm tối đa", item.PointsPossible?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> CourseDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = ApplyStudentScope(_context.CanvasCourses.AsNoTracking(), f);
        if (f.Has(f.Subject)) query = query.Where(item => item.Name != null && item.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.Period != null)
            query = query.Where(item => (item.StartAt == null || item.StartAt < f.Period.End) &&
                (item.EndAt == null || item.EndAt >= f.Period.Start));
        var rows = await query.ToListAsync();
        string Dim(Core.Entities.CanvasCourse item) => dimension switch
        {
            "subject" => Text(item.Name),
            "term" => Text(item.EnrollmentTermName),
            "workflowState" => Text(item.WorkflowState), _ => "—"
        };
        return rows.Where(item => Dim(item) == groupKey)
            .OrderBy(item => item.Name)
            .Select(item => ExplorerRecord(null, null, null, null, null, item.EndAt ?? item.StartAt,
                measure == "count" ? 1 : null, ("Khóa học", item.Name), ("Mã khóa học", item.CourseCode),
                ("Môn học", item.Name),
                ("Học kỳ", item.EnrollmentTermName),
                ("Trạng thái", item.WorkflowState)))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> ParentDrilldownRows(
        Filters f, string dimension, string groupKey)
    {
        var query = _context.SISStudentParents.AsNoTracking().Include(item => item.Student).AsQueryable();
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(item => item.Student.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item => item.Student.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.Student.Id == f.StudentId);
        var rows = await query.ToListAsync();
        string Dim(Core.Entities.SISStudentParent item) => dimension switch
        {
            "employmentSector" => Text(item.EmploymentSector), "ageGroup" => Text(item.AgeGroup),
            "relationship" => Text(item.Relationship), "occupation" => Text(item.Occupation),
            "gradeLevel" => Text(item.Student.GradeLevel), "homeroom" => Text(item.Student.Homeroom),
            "campus" => Text(item.Student.Campus), _ => "—"
        };
        return rows.Where(item => Dim(item) == groupKey)
            .OrderBy(item => item.FullName)
            .Select(item => ExplorerRecord(item.Student.Id, item.SISUserId,
                item.Student.FullName, item.Student.GradeLevel, item.Student.Homeroom,
                item.UpdatedAt ?? item.CreatedAt, 1, ("Phụ huynh", item.FullName),
                ("Quan hệ", item.Relationship), ("Nghề nghiệp", item.Occupation),
                ("Lĩnh vực", item.EmploymentSector), ("Nhóm tuổi", item.AgeGroup)))
            .ToList();
    }

    private static CanvasExploreDrilldownRowDto ExplorerRecord(
        int? studentId, string? sisUserId, string? fullName, string? gradeLevel,
        string? homeroom, DateTime? date, decimal? value,
        params (string Key, string? Value)[] fields) => new()
    {
        StudentId = studentId, SISUserId = sisUserId, FullName = fullName,
        GradeLevel = gradeLevel, Homeroom = homeroom, Date = date, Value = value,
        Fields = fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Key, field => field.Value)
    };

    // ---- Dataset row builders. Each returns (dimension key, measure value?) tuples. ----

    private sealed record SubRow(
        string? Subject, string? Term, string? WorkflowState, bool Late, bool Missing, bool? Excused,
        double? Score, double? PointsPossible, DateTime? SubmittedAt, DateTime? DueAt,
        string? GradeLevel, string? Homeroom);

    private async Task<List<(string Key, double? Value)>> SubmissionRows(Filters f, string dimension, string measure)
    {
        var query = _context.CanvasSubmissions.AsNoTracking().Where(s => s.StudentProfileId != null);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(s => s.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(s => s.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(s =>
            (s.Assignment!.Course!.CanvasGradeLevel ?? s.Student!.GradeLevel ??
             s.Student.CanvasGradeLevel) == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(s => s.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(s => s.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(s => s.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(s => s.Assignment!.Course!.Name != null &&
            s.Assignment.Course.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.Period != null)
            query = query.Where(item =>
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) >= f.Period.Start &&
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) < f.Period.End);

        var data = await query.Select(s => new SubRow(
            s.Assignment!.Course!.Name, s.Assignment.Course.EnrollmentTermName,
            s.WorkflowState, s.Late, s.Missing, s.Excused,
            s.Score, s.Assignment.PointsPossible, s.SubmittedAt,
            s.CachedDueDate ?? s.Assignment.DueAt,
            s.Assignment!.Course!.CanvasGradeLevel ?? s.Student!.GradeLevel ?? s.Student.CanvasGradeLevel,
            s.Student!.Homeroom)).ToListAsync();

        string Dim(SubRow s) => dimension switch
        {
            "subject" => Text(s.Subject),
            "term" => TermLabel(s.Term),
            "workflowState" => Text(s.WorkflowState),
            "gradeLevel" => Text(s.GradeLevel),
            "homeroom" => Text(s.Homeroom),
            "month" => Month(s.DueAt),
            "status" => SubmissionStatus(s.Missing, s.Excused, s.Late, s.SubmittedAt),
            _ => "—"
        };
        double? Measure(SubRow s) => measure switch
        {
            "count" => 1d,
            "avgScorePercent" => IsGraded(s.WorkflowState) && s.Score != null && s.PointsPossible > 0
                ? s.Score.Value / s.PointsPossible!.Value * 100 : null,
            "avgScore" => IsGraded(s.WorkflowState) ? s.Score : null,
            "sumPoints" => s.PointsPossible,
            _ => null
        };
        return data.Select(s => (Dim(s), Measure(s))).ToList();
    }

    private sealed record ScoreRow(
        string? CourseName, string? EnrollmentTermName, string? GradingPeriodTitle,
        double? FinalScore, double? CurrentScore, string? GradeLevel, string? Homeroom);

    private async Task<List<(string Key, double? Value)>> ScoreRows(Filters f, string dimension, string measure)
    {
        var query = _context.CanvasCourseScores.AsNoTracking()
            .Where(s => s.StudentProfileId != null);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(s => s.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(s => s.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(s =>
            (s.Enrollment!.Course!.CanvasGradeLevel ?? s.Student!.GradeLevel ??
             s.Student.CanvasGradeLevel) == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(s => s.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(s => s.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(s => s.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(s => s.Enrollment!.Course!.Name != null &&
            s.Enrollment.Course.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.TermAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.GradingPeriodTitle, f.TermAliases);

        var data = await query.Select(s => new ScoreRow(
            s.Enrollment!.Course!.Name,
            s.Enrollment.Course.EnrollmentTermName,
            s.GradingPeriodTitle,
            s.FinalScore, s.CurrentScore,
            s.Enrollment!.Course!.CanvasGradeLevel ?? s.Student!.GradeLevel ?? s.Student.CanvasGradeLevel,
            s.Student!.Homeroom)).ToListAsync();

        string Dim(ScoreRow s) => dimension switch
        {
            "subject" => Text(s.CourseName),
            "term" => Text(s.EnrollmentTermName),
            "gradingPeriod" => Text(s.GradingPeriodTitle),
            "courseName" => Text(s.CourseName),
            "gradeLevel" => Text(s.GradeLevel),
            "homeroom" => Text(s.Homeroom),
            _ => "—"
        };
        double? Measure(ScoreRow s) => measure switch
        {
            "count" => 1d,
            "avgFinalScore" => s.FinalScore,
            "avgCurrentScore" => s.CurrentScore,
            _ => null
        };
        return data.Select(s => (Dim(s), Measure(s))).ToList();
    }

    private sealed record AssignmentRow(
        string? Subject, string? Term, string? AssignmentGroupName, string? WorkflowState,
        DateTime? DueAt, double? PointsPossible, string? AcademicYear);

    private async Task<List<(string Key, double? Value)>> AssignmentRows(Filters f, string dimension, string measure)
    {
        var assignments = ApplyStudentScope(_context.CanvasAssignments.AsNoTracking(), f);
        if (f.Has(f.Subject)) assignments = assignments.Where(item => item.Course!.Name != null &&
            item.Course.Name.Trim() == f.Subject);
        assignments = ApplyAcademicYear(assignments, f.YearAliases);
        if (f.Period != null)
            assignments = assignments.Where(item => item.DueAt >= f.Period.Start && item.DueAt < f.Period.End);

        var data = await assignments.Select(item => new AssignmentRow(
            item.Course!.Name, item.Course.EnrollmentTermName,
            item.AssignmentGroupName, item.WorkflowState, item.DueAt, item.PointsPossible,
            item.Course.EnrollmentTermName)).ToListAsync();

        string Dim(AssignmentRow a) => dimension switch
        {
            "subject" => Text(a.Subject),
            "assignmentGroup" => Text(a.AssignmentGroupName),
            "workflowState" => Text(a.WorkflowState),
            "month" => Month(a.DueAt),
            _ => "—"
        };
        double? Measure(AssignmentRow a) => measure switch
        {
            "count" => 1d,
            "avgPointsPossible" => a.PointsPossible,
            _ => null
        };
        return data.Select(a => (Dim(a), Measure(a))).ToList();
    }

    private sealed record CourseRow(string? Subject, string? Term, string? TermName, string? WorkflowState);

    private async Task<List<(string Key, double? Value)>> CourseRows(Filters f, string dimension, string measure)
    {
        var query = ApplyStudentScope(_context.CanvasCourses.AsNoTracking(), f);
        if (f.Has(f.Subject)) query = query.Where(c => c.Name != null && c.Name.Trim() == f.Subject);
        query = ApplyAcademicYear(query, f.YearAliases);
        if (f.Period != null)
            query = query.Where(item => (item.StartAt == null || item.StartAt < f.Period.End) &&
                (item.EndAt == null || item.EndAt >= f.Period.Start));

        var data = await query.Select(c => new CourseRow(
            c.Name, c.EnrollmentTermName, c.EnrollmentTermName, c.WorkflowState)).ToListAsync();

        string Dim(CourseRow c) => dimension switch
        {
            "subject" => Text(c.Subject),
            "term" => Text(c.Term ?? c.TermName),
            "workflowState" => Text(c.WorkflowState),
            _ => "—"
        };
        return data.Select(c => (Dim(c), measure == "count" ? 1d : (double?)null)).ToList();
    }

    private sealed record ParentRow(
        string? Occupation, string? AgeGroup, string? EmploymentSector, string? Relationship,
        string? GradeLevel, string? Homeroom, string? Campus);

    private async Task<List<(string Key, double? Value)>> ParentRows(Filters f, string dimension, string measure)
    {
        // Parent/guardian demographics. Only the student-level filters apply (parents have no
        // year/term/subject dimension).
        var query = _context.SISStudentParents.AsNoTracking().AsQueryable();
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (f.Has(f.Campus)) query = query.Where(p => p.Student.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(p => p.Student.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(p => p.Student.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(p => p.Student.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(p => p.Student.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(p => p.Student.Id == f.StudentId);

        var data = await query.Select(p => new ParentRow(
            p.Occupation, p.AgeGroup, p.EmploymentSector, p.Relationship,
            p.Student.GradeLevel, p.Student.Homeroom, p.Student.Campus)).ToListAsync();

        string Dim(ParentRow p) => dimension switch
        {
            "employmentSector" => Text(p.EmploymentSector),
            "ageGroup" => Text(p.AgeGroup),
            "relationship" => Text(p.Relationship),
            "occupation" => Text(p.Occupation),
            "gradeLevel" => Text(p.GradeLevel),
            "homeroom" => Text(p.Homeroom),
            "campus" => Text(p.Campus),
            _ => "—"
        };
        return data.Select(p => (Dim(p), measure == "count" ? 1d : (double?)null)).ToList();
    }

    // ---- Helpers ----

    private static string Aggregation(string measure) => measure switch
    {
        "count" => "count",
        "sumPoints" => "sum",
        _ => "average"
    };

    private static bool IsGraded(string? state) =>
        string.Equals(state, "graded", StringComparison.OrdinalIgnoreCase);

    private static string SubmissionStatus(bool missing, bool? excused, bool late, DateTime? submittedAt)
    {
        if (excused == true) return "Excused";
        if (missing) return "Missing";
        if (late) return "Late";
        return submittedAt != null ? "On time" : "Not submitted";
    }

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    private static string Month(DateTime? date) => date?.ToString("yyyy-MM") ?? "—";
    private static string TermLabel(string? term) => Text(term);

    private static string ExploreCacheKey(params string?[] values) =>
        "dashboard:canvas-explore:" + string.Join('|', values.Select(value =>
        {
            var normalized = value?.Trim().ToLowerInvariant() ?? "";
            return $"{normalized.Length}:{normalized}";
        }));

    private static CanvasExploreOptionsDto BuildOptions() => new()
    {
        Datasets =
        [
            new CanvasExploreDatasetDto
            {
                Key = "submissions", Label = "Assignment submissions",
                Dimensions =
                [
                    Field("subject", "Canvas course name"), Field("status", "Calculated status from Canvas flags"),
                    Field("term", "Canvas enrollment term name"), Field("workflowState", "Canvas workflow_state"),
                    Field("gradeLevel", "SIS grade level"), Field("homeroom", "SIS homeroom"),
                    Field("month", "Month of Canvas due date")
                ],
                Measures =
                [
                    Field("count", "Number of submissions", "submissions"),
                    Field("avgScorePercent", "Average submission.score / assignment.points_possible", "%"),
                    Field("avgScore", "Average Canvas submission.score", "pts"),
                    Field("sumPoints", "Sum of related Canvas assignment.points_possible", "pts")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "scores", Label = "Course grades",
                Dimensions =
                [
                    Field("subject", "Canvas course name"), Field("courseName", "Canvas course name"),
                    Field("term", "Canvas enrollment term name"), Field("gradingPeriod", "Canvas grading period title"),
                    Field("gradeLevel", "SIS grade level"), Field("homeroom", "SIS homeroom")
                ],
                Measures =
                [
                    Field("avgFinalScore", "Average Canvas grades.final_score", "%"),
                    Field("avgCurrentScore", "Average Canvas grades.current_score", "%"),
                    Field("count", "Number of grade records", "records")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "assignments", Label = "Assignments (catalogue)",
                Dimensions =
                [
                    Field("subject", "Canvas course name"), Field("assignmentGroup", "Canvas assignment-group name"),
                    Field("workflowState", "Canvas workflow_state"), Field("month", "Month of Canvas due_at")
                ],
                Measures =
                [
                    Field("count", "Number of assignments", "assignments"),
                    Field("avgPointsPossible", "Average Canvas points_possible", "pts")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "courses", Label = "Courses (catalogue)",
                Dimensions =
                [
                    Field("subject", "Canvas course name"), Field("term", "Canvas enrollment term name"), Field("workflowState", "Canvas workflow_state")
                ],
                Measures = [Field("count", "Number of courses", "courses")]
            },
            new CanvasExploreDatasetDto
            {
                Key = "parents", Label = "Parents & guardians",
                Dimensions =
                [
                    Field("employmentSector", "Employment sector"), Field("ageGroup", "Age group"),
                    Field("occupation", "Occupation"), Field("relationship", "Relationship"),
                    Field("gradeLevel", "Student grade level"), Field("homeroom", "Student homeroom"),
                    Field("campus", "Campus")
                ],
                Measures = [Field("count", "Number of parents/guardians", "people")]
            }
        ]
    };

    private static CanvasExploreFieldDto Field(string key, string label, string unit = "") =>
        new() { Key = key, Label = label, Unit = unit };

    private IQueryable<CanvasCourse> ApplyStudentScope(
        IQueryable<CanvasCourse> query, Filters filters)
    {
        if (_userAccess.Current.RestrictsStudents)
        {
            var allowedStudentIds = _userAccess.ApplyStudentScope(
                _context.StudentProfiles.AsNoTracking()).Select(student => student.Id);
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null && allowedStudentIds.Contains(item.StudentProfileId.Value)));
        }
        if (filters.Has(filters.Campus))
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Campus == filters.Campus));
        if (filters.Has(filters.Level))
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.EducationLevel == filters.Level));
        if (filters.Has(filters.Grade))
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null &&
                (course.CanvasGradeLevel ?? item.Student!.GradeLevel ?? item.Student.CanvasGradeLevel) == filters.Grade));
        if (filters.Has(filters.Homeroom))
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Homeroom == filters.Homeroom));
        if (filters.Has(filters.Program))
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Program == filters.Program));
        if (filters.StudentId.HasValue)
            query = query.Where(course => course.Enrollments.Any(item =>
                item.StudentProfileId == filters.StudentId));
        return query;
    }

    private IQueryable<CanvasAssignment> ApplyStudentScope(
        IQueryable<CanvasAssignment> query, Filters filters)
    {
        if (_userAccess.Current.RestrictsStudents)
        {
            var allowedStudentIds = _userAccess.ApplyStudentScope(
                _context.StudentProfiles.AsNoTracking()).Select(student => student.Id);
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null && allowedStudentIds.Contains(item.StudentProfileId.Value)));
        }
        if (filters.Has(filters.Campus))
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Campus == filters.Campus));
        if (filters.Has(filters.Level))
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.EducationLevel == filters.Level));
        if (filters.Has(filters.Grade))
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null &&
                (assignment.Course.CanvasGradeLevel ?? item.Student!.GradeLevel ??
                 item.Student.CanvasGradeLevel) == filters.Grade));
        if (filters.Has(filters.Homeroom))
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Homeroom == filters.Homeroom));
        if (filters.Has(filters.Program))
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId != null && item.Student!.Program == filters.Program));
        if (filters.StudentId.HasValue)
            query = query.Where(assignment => assignment.Course!.Enrollments.Any(item =>
                item.StudentProfileId == filters.StudentId));
        return query;
    }

    private async Task<Filters> BuildFilters(
        string? year, string? term, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject)
    {
        var hasYear = HasValue(year);
        var hasTerm = HasValue(term);
        var yearAliases = new List<string>();
        var termAliases = new List<string>();

        if (hasYear)
        {
            var normalizedYear = DashboardPeriodNormalizer.AcademicYear(year!);
            yearAliases = (await _context.CanvasCourses.AsNoTracking()
                    .Where(item => item.EnrollmentTermName != null)
                    .Select(item => item.EnrollmentTermName!)
                    .Distinct()
                    .ToListAsync())
                .Where(value => DashboardPeriodNormalizer.AcademicYear(value)
                    .Equals(normalizedYear, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (yearAliases.Count == 0) yearAliases.Add(year!);
        }

        if (hasTerm)
        {
            var normalizedTerm = DashboardPeriodNormalizer.Term(term!);
            termAliases = (await _context.CanvasCourseScores.AsNoTracking()
                    .Where(item => item.GradingPeriodTitle != null)
                    .Select(item => item.GradingPeriodTitle!)
                    .Distinct()
                    .ToListAsync())
                .Where(value => DashboardPeriodNormalizer.Term(value)
                    .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (termAliases.Count == 0) termAliases.Add(term!);
        }

        PeriodRange? period = null;
        if (hasTerm)
        {
            var normalizedYear = hasYear ? DashboardPeriodNormalizer.AcademicYear(year!) : null;
            var normalizedTerm = DashboardPeriodNormalizer.Term(term!);
            var configured = (await _context.AcademicPeriods.AsNoTracking().ToListAsync())
                .Where(item => normalizedYear == null || DashboardPeriodNormalizer.AcademicYear(item.AcademicYear)
                    .Equals(normalizedYear, StringComparison.OrdinalIgnoreCase))
                .Where(item => DashboardPeriodNormalizer.Term(item.Term)
                    .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.IsCurrent)
                .ThenByDescending(item => item.StartDate)
                .FirstOrDefault();
            if (configured != null)
            {
                period = new PeriodRange(configured.StartDate, configured.EndDate);
            }
            else if (normalizedYear is not null && DashboardPeriodNormalizer.IsAcademicYear(normalizedYear))
            {
                var (start, end) = DashboardPeriodNormalizer.PeriodRange(normalizedYear, normalizedTerm);
                period = new PeriodRange(start, end);
            }
        }

        return new Filters(Clean(campus), Clean(level), Clean(grade), Clean(homeroom), studentId,
            Clean(program), Clean(subject),
            yearAliases, termAliases, period);
    }

    private static IQueryable<CanvasSubmission> ApplyAcademicYear(
        IQueryable<CanvasSubmission> query, IReadOnlyCollection<string> aliases)
    {
        if (aliases.Count == 0) return query;
        var year = DashboardPeriodNormalizer.AcademicYear(aliases.First());
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(year);
        return query.WhereMatchesAny(item => item.Assignment!.Course!.EnrollmentTermName, aliases,
                item => (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= start &&
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < end)
            .Where(item =>
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) == null ||
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= start &&
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < end);
    }

    private static IQueryable<CanvasCourseScore> ApplyAcademicYear(
        IQueryable<CanvasCourseScore> query, IReadOnlyCollection<string> aliases)
    {
        if (aliases.Count == 0) return query;
        var year = DashboardPeriodNormalizer.AcademicYear(aliases.First());
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(year);
        return query.WhereMatchesAny(item => item.Enrollment!.Course!.EnrollmentTermName, aliases,
            item => (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= start &&
                (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < end);
    }

    private static IQueryable<CanvasAssignment> ApplyAcademicYear(
        IQueryable<CanvasAssignment> query, IReadOnlyCollection<string> aliases)
    {
        if (aliases.Count == 0) return query;
        var year = DashboardPeriodNormalizer.AcademicYear(aliases.First());
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(year);
        return query.WhereMatchesAny(item => item.Course!.EnrollmentTermName, aliases,
                item => item.DueAt >= start && item.DueAt < end)
            .Where(item => item.DueAt == null || item.DueAt >= start && item.DueAt < end);
    }

    private static IQueryable<CanvasCourse> ApplyAcademicYear(
        IQueryable<CanvasCourse> query, IReadOnlyCollection<string> aliases)
    {
        if (aliases.Count == 0) return query;
        var year = DashboardPeriodNormalizer.AcademicYear(aliases.First());
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(year);
        return query.WhereMatchesAny(item => item.EnrollmentTermName, aliases,
            item => item.StartAt < end && item.EndAt >= start);
    }

    private static bool HasValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value) => HasValue(value) ? value!.Trim() : null;

    private sealed record PeriodRange(DateTime Start, DateTime End);

    private sealed record Filters(
        string? Campus, string? Level, string? Grade, string? Homeroom, int? StudentId,
        string? Program, string? Subject, List<string> YearAliases, List<string> TermAliases,
        PeriodRange? Period)
    {
        public bool Has(string? value) => HasValue(value);
    }
}
