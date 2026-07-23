using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
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

    public CanvasExploreController(AppDbContext context)
    {
        _context = context;
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

        var f = new Filters(year, term, campus, level, grade, homeroom, studentId, program, subject);
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

        return Ok(new CanvasExploreResultDto
        {
            Dataset = dataset,
            Dimension = dimension,
            Measure = measure,
            Unit = measureOption.Unit,
            Aggregation = aggregation,
            Rows = grouped
        });
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
        var filters = new Filters(year, term, campus, level, grade, homeroom, studentId, program, subject);
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
        if (f.Has(f.Campus)) query = query.Where(item => item.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item => item.Student!.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(item => item.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(item => item.AcademicYear == f.Year);
        if (f.Has(f.Term)) query = query.Where(item => item.Term == f.Term);

        var rows = await query.Select(item => new
        {
            item.StudentProfileId, item.Student!.StudentCode, item.Student.FullName,
            item.Student.GradeLevel, item.Student.Homeroom, item.Subject, item.Term,
            item.WorkflowState, item.Late, item.Missing, item.Excused, item.Score,
            item.PointsPossible, item.SubmittedAt, item.DueAt,
            AssignmentTitle = item.Assignment != null ? item.Assignment.Title : null
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
            .Select(item => ExplorerRecord(item.StudentProfileId, item.StudentCode, item.FullName,
                item.GradeLevel, item.Homeroom, item.SubmittedAt ?? item.DueAt, Value(item),
                ("Bài tập", item.AssignmentTitle), ("Môn học", item.Subject),
                ("Trạng thái", SubmissionStatus(item.Missing, item.Excused, item.Late, item.SubmittedAt)),
                ("Điểm", item.Score?.ToString("0.##")), ("Điểm tối đa", item.PointsPossible?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> ScoreDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = _context.CanvasCourseScores.AsNoTracking().Where(item => item.CourseScore && item.StudentProfileId != null);
        if (f.Has(f.Campus)) query = query.Where(item => item.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item => item.Student!.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(item => item.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(item => item.AcademicYear == f.Year);
        if (f.Has(f.Term)) query = query.Where(item => item.Term == f.Term);
        var rows = await query.Select(item => new
        {
            item.StudentProfileId, item.Student!.StudentCode, item.Student.FullName,
            item.Student.GradeLevel, item.Student.Homeroom, item.Subject, item.Term,
            item.CourseName, item.GradingPeriodTitle, item.FinalScore, item.CurrentScore,
            Date = item.UpdatedAt ?? item.CreatedAt
        }).ToListAsync();
        string Dim(dynamic item) => dimension switch
        {
            "subject" => Text(item.Subject), "term" => TermLabel(item.Term),
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
            .Select(item => ExplorerRecord(item.StudentProfileId, item.StudentCode, item.FullName,
                item.GradeLevel, item.Homeroom, item.Date, Value(item),
                ("Khóa học", item.CourseName), ("Môn học", item.Subject),
                ("Kỳ chấm điểm", item.GradingPeriodTitle),
                ("Điểm hiện tại", item.CurrentScore?.ToString("0.##")),
                ("Điểm cuối kỳ", item.FinalScore?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> AssignmentDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = from assignment in _context.CanvasAssignments.AsNoTracking()
                    join course in _context.CanvasCourses.AsNoTracking()
                        on assignment.CanvasCourseId equals course.CanvasCourseId
                    select new { assignment.Title, assignment.AssignmentGroupName, assignment.WorkflowState,
                        assignment.DueAt, assignment.PointsPossible, course.Subject, course.AcademicYear };
        if (f.Has(f.Subject)) query = query.Where(item => item.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(item => item.AcademicYear == f.Year);
        var rows = await query.ToListAsync();
        string Dim(dynamic item) => dimension switch
        {
            "subject" => Text(item.Subject), "assignmentGroup" => Text(item.AssignmentGroupName),
            "workflowState" => Text(item.WorkflowState), "month" => Month(item.DueAt), _ => "—"
        };
        decimal? Value(dynamic item) => measure == "count" ? 1m : (decimal?)item.PointsPossible;
        return rows.Where(item => Dim(item) == groupKey && Value(item) != null)
            .OrderByDescending(item => item.DueAt)
            .Select(item => ExplorerRecord(null, null, null, null, null, item.DueAt, Value(item),
                ("Bài tập", item.Title), ("Môn học", item.Subject),
                ("Nhóm bài tập", item.AssignmentGroupName), ("Trạng thái", item.WorkflowState),
                ("Điểm tối đa", item.PointsPossible?.ToString("0.##"))))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> CourseDrilldownRows(
        Filters f, string dimension, string measure, string groupKey)
    {
        var query = _context.CanvasCourses.AsNoTracking().AsQueryable();
        if (f.Has(f.Subject)) query = query.Where(item => item.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(item => item.AcademicYear == f.Year);
        var rows = await query.ToListAsync();
        string Dim(Core.Entities.CanvasCourse item) => dimension switch
        {
            "subject" => Text(item.Subject), "term" => Text(item.Term ?? item.TermName),
            "workflowState" => Text(item.WorkflowState), _ => "—"
        };
        return rows.Where(item => Dim(item) == groupKey)
            .OrderBy(item => item.Name)
            .Select(item => ExplorerRecord(null, null, null, null, null, item.UpdatedAt ?? item.CreatedAt,
                measure == "count" ? 1 : null, ("Khóa học", item.Name), ("Mã khóa học", item.CourseCode),
                ("Môn học", item.Subject), ("Học kỳ", item.Term ?? item.TermName),
                ("Trạng thái", item.WorkflowState)))
            .ToList();
    }

    private async Task<List<CanvasExploreDrilldownRowDto>> ParentDrilldownRows(
        Filters f, string dimension, string groupKey)
    {
        var query = _context.SISStudentParents.AsNoTracking().Include(item => item.Student).AsQueryable();
        if (f.Has(f.Campus)) query = query.Where(item => item.Student.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(item => item.Student.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(item => item.Student.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(item => item.Student.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(item => item.Student.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(item => item.StudentProfileId == f.StudentId);
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
            .Select(item => ExplorerRecord(item.StudentProfileId, item.Student.StudentCode,
                item.Student.FullName, item.Student.GradeLevel, item.Student.Homeroom,
                item.UpdatedAt ?? item.CreatedAt, 1, ("Phụ huynh", item.FullName),
                ("Quan hệ", item.Relationship), ("Nghề nghiệp", item.Occupation),
                ("Lĩnh vực", item.EmploymentSector), ("Nhóm tuổi", item.AgeGroup)))
            .ToList();
    }

    private static CanvasExploreDrilldownRowDto ExplorerRecord(
        int? studentId, string? studentCode, string? fullName, string? gradeLevel,
        string? homeroom, DateTime? date, decimal? value,
        params (string Key, string? Value)[] fields) => new()
    {
        StudentId = studentId, StudentCode = studentCode, FullName = fullName,
        GradeLevel = gradeLevel, Homeroom = homeroom, Date = date, Value = value,
        Fields = fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Key, field => field.Value)
    };

    // ---- Dataset row builders. Each returns (dimension key, measure value?) tuples. ----

    private sealed record SubRow(
        string? Subject, string? Term, string? WorkflowState, bool Late, bool Missing, bool Excused,
        double? Score, double? PointsPossible, DateTime? SubmittedAt, DateTime? DueAt,
        string? GradeLevel, string? Homeroom);

    private async Task<List<(string Key, double? Value)>> SubmissionRows(Filters f, string dimension, string measure)
    {
        var query = _context.CanvasSubmissions.AsNoTracking().Where(s => s.StudentProfileId != null);
        if (f.Has(f.Campus)) query = query.Where(s => s.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(s => s.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(s => s.Student!.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(s => s.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(s => s.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(s => s.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(s => s.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(s => s.AcademicYear == f.Year);
        if (f.Has(f.Term)) query = query.Where(s => s.Term == f.Term);

        var data = await query.Select(s => new SubRow(
            s.Subject, s.Term, s.WorkflowState, s.Late, s.Missing, s.Excused,
            s.Score, s.PointsPossible, s.SubmittedAt, s.DueAt,
            s.Student!.GradeLevel, s.Student!.Homeroom)).ToListAsync();

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
        string? Subject, string? Term, string? GradingPeriodTitle, string? CourseName,
        double? FinalScore, double? CurrentScore, string? GradeLevel, string? Homeroom);

    private async Task<List<(string Key, double? Value)>> ScoreRows(Filters f, string dimension, string measure)
    {
        var query = _context.CanvasCourseScores.AsNoTracking()
            .Where(s => s.CourseScore && s.StudentProfileId != null);
        if (f.Has(f.Campus)) query = query.Where(s => s.Student!.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(s => s.Student!.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(s => s.Student!.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(s => s.Student!.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(s => s.Student!.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(s => s.StudentProfileId == f.StudentId);
        if (f.Has(f.Subject)) query = query.Where(s => s.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(s => s.AcademicYear == f.Year);
        if (f.Has(f.Term)) query = query.Where(s => s.Term == f.Term);

        var data = await query.Select(s => new ScoreRow(
            s.Subject, s.Term, s.GradingPeriodTitle, s.CourseName, s.FinalScore, s.CurrentScore,
            s.Student!.GradeLevel, s.Student!.Homeroom)).ToListAsync();

        string Dim(ScoreRow s) => dimension switch
        {
            "subject" => Text(s.Subject),
            "term" => TermLabel(s.Term),
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
        // Assignments are catalogue rows; join their course for the subject dimension.
        var query = from a in _context.CanvasAssignments.AsNoTracking()
                    join c in _context.CanvasCourses.AsNoTracking() on a.CanvasCourseId equals c.CanvasCourseId
                    select new AssignmentRow(
                        c.Subject, c.Term ?? c.TermName, a.AssignmentGroupName,
                        a.WorkflowState, a.DueAt, a.PointsPossible, c.AcademicYear);
        if (f.Has(f.Subject)) query = query.Where(a => a.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(a => a.AcademicYear == f.Year);

        var data = await query.ToListAsync();

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
        var query = _context.CanvasCourses.AsNoTracking().AsQueryable();
        if (f.Has(f.Subject)) query = query.Where(c => c.Subject == f.Subject);
        if (f.Has(f.Year)) query = query.Where(c => c.AcademicYear == f.Year);

        var data = await query.Select(c => new CourseRow(c.Subject, c.Term, c.TermName, c.WorkflowState)).ToListAsync();

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
        if (f.Has(f.Campus)) query = query.Where(p => p.Student.Campus == f.Campus);
        if (f.Has(f.Level)) query = query.Where(p => p.Student.EducationLevel == f.Level);
        if (f.Has(f.Grade)) query = query.Where(p => p.Student.GradeLevel == f.Grade);
        if (f.Has(f.Homeroom)) query = query.Where(p => p.Student.Homeroom == f.Homeroom);
        if (f.Has(f.Program)) query = query.Where(p => p.Student.Program == f.Program);
        if (f.StudentId.HasValue) query = query.Where(p => p.StudentProfileId == f.StudentId);

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

    private static string SubmissionStatus(bool missing, bool excused, bool late, DateTime? submittedAt)
    {
        if (excused) return "Excused";
        if (missing) return "Missing";
        if (late) return "Late";
        return submittedAt != null ? "On time" : "Not submitted";
    }

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value!;
    private static string Month(DateTime? date) => date?.ToString("yyyy-MM") ?? "—";
    private static string TermLabel(string? term) => term switch
    {
        "hk1" => "Semester 1",
        "hk2" => "Semester 2",
        null or "" => "—",
        _ => term!
    };

    private static CanvasExploreOptionsDto BuildOptions() => new()
    {
        Datasets =
        [
            new CanvasExploreDatasetDto
            {
                Key = "submissions", Label = "Assignment submissions",
                Dimensions =
                [
                    Field("subject", "Subject"), Field("status", "Submission status"),
                    Field("term", "Term"), Field("workflowState", "Workflow state"),
                    Field("gradeLevel", "Grade level"), Field("homeroom", "Homeroom"),
                    Field("month", "Month (due)")
                ],
                Measures =
                [
                    Field("count", "Number of submissions", "submissions"),
                    Field("avgScorePercent", "Average score", "%"),
                    Field("avgScore", "Average points", "pts"),
                    Field("sumPoints", "Total points possible", "pts")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "scores", Label = "Course grades",
                Dimensions =
                [
                    Field("subject", "Subject"), Field("courseName", "Course"),
                    Field("term", "Term"), Field("gradingPeriod", "Grading period"),
                    Field("gradeLevel", "Grade level"), Field("homeroom", "Homeroom")
                ],
                Measures =
                [
                    Field("avgFinalScore", "Average final grade", "%"),
                    Field("avgCurrentScore", "Average current grade", "%"),
                    Field("count", "Number of grade records", "records")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "assignments", Label = "Assignments (catalogue)",
                Dimensions =
                [
                    Field("subject", "Subject"), Field("assignmentGroup", "Assignment group"),
                    Field("workflowState", "Workflow state"), Field("month", "Month (due)")
                ],
                Measures =
                [
                    Field("count", "Number of assignments", "assignments"),
                    Field("avgPointsPossible", "Average points possible", "pts")
                ]
            },
            new CanvasExploreDatasetDto
            {
                Key = "courses", Label = "Courses (catalogue)",
                Dimensions =
                [
                    Field("subject", "Subject"), Field("term", "Term"), Field("workflowState", "Workflow state")
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

    private sealed record Filters(
        string? Year, string? Term, string? Campus, string? Level, string? Grade,
        string? Homeroom, int? StudentId, string? Program, string? Subject)
    {
        public bool Has(string? value) =>
            !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
    }
}
