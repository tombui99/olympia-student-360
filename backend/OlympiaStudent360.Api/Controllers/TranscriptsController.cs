using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Text.Json;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Caching.Memory;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/transcripts")]
[ApiController]
[Authorize]
public class TranscriptsController(
    AppDbContext context,
    UserAccessService userAccess,
    TranscriptSubjectResolver subjectResolver,
    IMemoryCache? cache = null,
    InFlightRequestCoalescer? requests = null) : ControllerBase
{
    private static readonly JsonSerializerOptions RecordJsonOptions = new(JsonSerializerDefaults.Web);
    private bool CanEditRecord => userAccess.Current.CanAccessAcademic &&
        userAccess.Current.Role is "Admin" or "SchoolAdministration" or "Principal" or "Teacher";

    [HttpPut("students/{studentId:int}/record")]
    [RequestSizeLimit(256_000)]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<TranscriptRecordDto>> SaveRecord(int studentId, TranscriptRecordDto model)
    {
        if (!CanEditRecord) return Forbid();
        if (!await VisibleStudents().AnyAsync(student => student.Id == studentId)) return NotFound();
        var validation = TranscriptRules.Validate(model);
        if (validation != null) return BadRequest(new { Message = validation });
        var saved = await context.StudentTranscriptRecords.FirstOrDefaultAsync(row => row.StudentProfileId == studentId);
        if ((saved?.Revision ?? 0) != model.Revision)
            return Conflict(new { Message = "This transcript was updated by another user. Reload before saving." });
        if (saved == null)
        {
            saved = new StudentTranscriptRecord { StudentProfileId = studentId };
            context.StudentTranscriptRecords.Add(saved);
        }
        foreach (var year in model.Years)
            year.GradeLevel = string.IsNullOrWhiteSpace(year.GradeLevel) ? null : year.GradeLevel.Trim();
        model.Revision++;
        model.TranscriptName = string.IsNullOrWhiteSpace(model.TranscriptName) ? null : model.TranscriptName.Trim();
        model.Notes = model.Notes?.Trim();
        saved.RecordJson = JsonSerializer.Serialize(model, RecordJsonOptions);
        saved.Revision = model.Revision;
        saved.UpdatedAt = DateTime.UtcNow;
        saved.UpdatedBy = User.Identity?.Name ?? userAccess.Current.UserId ?? "Staff";
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { Message = "The transcript could not be saved. Reload before trying again." });
        }
        catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict(new { Message = "The transcript was created by another user. Reload before saving." });
        }
        return Ok(model);
    }

    private static TranscriptRecordDto ReadRecord(StudentTranscriptRecord? record)
    {
        if (record == null) return new();
        var value = JsonSerializer.Deserialize<TranscriptRecordDto>(record.RecordJson, RecordJsonOptions) ?? new();
        value.Revision = record.Revision;
        return value;
    }

    private IQueryable<CanvasCourseScore> WithAssessmentEvidence(IQueryable<CanvasCourseScore> scores)
    {
        // Materialize one set of qualified IDs in SQL. An OR + correlated EXISTS makes MySQL
        // repeat a student-wide submission lookup for every score and for every aggregate query.
        var overridden = scores.Where(score => score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null)
            .Select(score => score.Id);
        var graded = context.CanvasSubmissions.AsNoTracking().Where(submission => submission.StudentProfileId != null && submission.Score != null &&
            submission.Excused != true && submission.Assignment != null &&
            !submission.Assignment.OmitFromFinalGrade && submission.Assignment.PointsPossible > 0);
        var supported = from score in scores
            where score.EnrollmentOverrideScore == null && score.GradesOverrideScore == null
            join submission in graded on new { Student = score.StudentProfileId!.Value, Course = score.Enrollment!.CanvasCourseId }
                equals new { Student = submission.StudentProfileId!.Value, Course = submission.CanvasCourseId }
            where (score.GradingPeriodStartDate == null ||
                    (submission.Assignment!.DueAt ?? submission.CachedDueDate ?? submission.GradedAt) >= score.GradingPeriodStartDate) &&
                (score.GradingPeriodEndDate == null ||
                    (submission.Assignment!.DueAt ?? submission.CachedDueDate ?? submission.GradedAt) <= score.GradingPeriodEndDate)
            select score.Id;
        return scores.Join(overridden.Union(supported), score => score.Id, id => id, (score, _) => score);
    }

    private static readonly Regex ClassNamePattern = new(
        @"(?<![A-Za-z0-9])(?<class>\d{1,2}[A-Za-z]{1,3}\d{1,2}(?:\.\d+)?)(?![A-Za-z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Expression<Func<CanvasCourseScore, bool>> HasUsableTranscriptScore =
        score => score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
            score.CurrentScore != null ||
            score.FinalScore != null && (score.FinalScore != 0 ||
                score.CurrentGrade != null || score.FinalGrade != null);

    private sealed record AggregateRow(
        string AcademicYear,
        string GradeLevel,
        string SubjectCode,
        string Subject,
        long CanvasCourseId,
        string ClassName,
        string CourseName,
        string Term,
        double? Average,
        int RecordCount);

    [HttpGet("students")]
    public async Task<ActionResult<List<TranscriptStudentDto>>> GetTranscriptStudents(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 10)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        var term = search?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new List<TranscriptStudentDto>());

        term = term[..Math.Min(term.Length, 100)];
        limit = Math.Clamp(limit, 1, 20);
        var pattern = $"%{EscapeLikePattern(term)}%";
        var students = await VisibleStudents()
            .Where(student => context.CanvasCourseScores.Any(score =>
                score.StudentProfileId == student.Id &&
                score.Enrollment!.Course!.EnrollmentTermName != null &&
                score.Enrollment.Course.EnrollmentTermName != "" &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null)))
            .Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.SISUserId, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"))
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.SISUserId)
            .Take(limit)
            .Select(student => new TranscriptStudentDto
            {
                Id = student.Id,
                SISUserId = student.SISUserId,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
                Homeroom = student.Homeroom
            })
            .ToListAsync();
        return Ok(students);
    }

    [HttpGet("student-directory")]
    public async Task<ActionResult<TranscriptStudentsPageDto>> GetTranscriptStudentDirectory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 50);
        var term = search?.Trim();
        if (term?.Length > 100) term = term[..100];

        var query = VisibleStudents()
            .Where(student => context.CanvasCourseScores.Any(score =>
                score.StudentProfileId == student.Id &&
                score.Enrollment!.Course!.EnrollmentTermName != null &&
                score.Enrollment.Course.EnrollmentTermName != "" &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null)));

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.SISUserId, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.SISUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(student => new TranscriptStudentDto
            {
                Id = student.Id,
                SISUserId = student.SISUserId,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
                Homeroom = student.Homeroom
            })
            .ToListAsync();

        return Ok(new TranscriptStudentsPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

    [HttpGet("catalog")]
    public async Task<ActionResult<TranscriptCatalogDto>> GetCatalog(CancellationToken cancellationToken = default)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        if (!cancellationToken.CanBeCanceled) cancellationToken = HttpContext?.RequestAborted ?? default;
        var key = $"transcript-catalog:v1:{userAccess.Current.CacheKey}";
        if (cache != null && cache.TryGetValue<TranscriptCatalogDto>(key, out var cached)) return Ok(cached);
        // Selection metadata comes from the course directory. No marks, submissions or GPA are read.
        var courses = await userAccess.ApplyStudentScope(context.CanvasEnrollments.AsNoTracking(), row => row.Student)
            .Where(row => row.Student != null && row.Course != null)
            .Select(row => new { row.CanvasCourseId, row.Course!.SisCourseId, row.Course.CourseCode,
                row.Course.Name, row.Course.CanvasGradeLevel, row.Course.EnrollmentTermName, row.Course.EndAt })
            .Distinct().ToListAsync(cancellationToken);
        var resolved = courses.Select(course => new
        {
            Subject = subjectResolver.Resolve(course.CanvasCourseId, course.SisCourseId, course.CourseCode, course.Name),
            Grade = TranscriptRules.HistoricalGrade(course.Name, course.CourseCode, course.CanvasGradeLevel),
            Year = DashboardPeriodNormalizer.AcademicYear(course.EnrollmentTermName ?? "", course.EndAt)
        }).ToList();
        var result = new TranscriptCatalogDto
        {
            Years = resolved.Where(row => DashboardPeriodNormalizer.IsAcademicYear(row.Year) && row.Grade != null)
                .GroupBy(row => row.Year).OrderByDescending(group => StartYear(group.Key))
                .Select(group => new TranscriptCatalogYearDto { AcademicYear = group.Key,
                    GradeLevels = group.Select(row => row.Grade!).Distinct().OrderBy(int.Parse).ToList() }).ToList(),
            SubjectOptions = resolved.GroupBy(row => row.Subject.Code, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => subjectResolver.SortOrder(group.Key)).ThenBy(group => group.First().Subject.Name)
                .Select(group => new TranscriptSubjectOptionDto { SubjectCode = group.Key, Subject = group.First().Subject.Name,
                    GradeLevels = group.Where(row => row.Grade != null).Select(row => row.Grade!).Distinct().OrderBy(int.Parse).ToList() }).ToList()
        };
        cache?.Set(key, result, AppMemoryCache.Entry(TimeSpan.FromSeconds(30), size: 2));
        return Ok(result);
    }

    [HttpGet("overall")]
    public async Task<ActionResult<SchoolTranscriptDto>> GetOverallTranscript(
        [FromQuery] int? studentId = null,
        [FromQuery] int? subjectPage = null,
        [FromQuery] int? subjectPageSize = null,
        [FromQuery] string[]? excludedSubjects = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? gradeLevel = null,
        CancellationToken cancellationToken = default)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        if (!cancellationToken.CanBeCanceled) cancellationToken = HttpContext?.RequestAborted ?? default;
        academicYear = academicYear?.Trim();
        gradeLevel = gradeLevel?.Trim();
        if (!studentId.HasValue && (string.IsNullOrWhiteSpace(academicYear) || string.IsNullOrWhiteSpace(gradeLevel)))
            return BadRequest(new { Message = "Choose an academic year and grade before loading a summary, or choose a student." });
        if (academicYear != null && (!DashboardPeriodNormalizer.IsAcademicYear(academicYear) ||
            DashboardPeriodNormalizer.AcademicYear(academicYear) != academicYear) ||
            gradeLevel != null && (!int.TryParse(gradeLevel, out var grade) || grade is < 1 or > 12))
            return BadRequest(new { Message = "Invalid academic year or grade." });
        SchoolTranscriptDto? snapshot;
        if (studentId.HasValue || cache == null)
        {
            snapshot = await BuildOverallTranscript(studentId, academicYear, gradeLevel, cancellationToken);
        }
        else
        {
            // One small scalar query observes school-authored edits, roster imports and ETL
            // changes across processes. Cached pages never bypass a changed access scope.
            var version = await context.StudentProfiles.AsNoTracking().OrderBy(student => student.Id).Select(_ => new
            {
                Records = context.StudentTranscriptRecords.Sum(row => (long?)row.Revision) ?? 0,
                Imported = context.ImportedReportChanges.Max(row => (int?)row.Id) ?? 0,
                ImportedBuild = context.ImportedReportBuilds.Max(row => (DateTime?)row.CompletedAtUtc),
                Canvas = context.CanvasReportCourseChanges.Max(row => (DateTime?)row.ChangedAtUtc),
                CanvasBuild = context.CanvasDashboardAggregateBuilds.Max(row => (DateTime?)row.CompletedAtUtc),
                Sync = context.CanvasSyncRuns.Where(row => row.CompletedAtUtc != null).Max(row => (int?)row.Id) ?? 0
            }).FirstOrDefaultAsync(cancellationToken);
            var key = $"transcripts-school:v4:{userAccess.Current.CacheKey}:{academicYear}:{gradeLevel}:{version?.Records}:{version?.Imported}:" +
                $"{version?.ImportedBuild?.Ticks}:{version?.Canvas?.Ticks}:{version?.CanvasBuild?.Ticks}:{version?.Sync}";
            if (!cache.TryGetValue(key, out snapshot))
            {
                async Task<SchoolTranscriptDto?> Build(CancellationToken token)
                {
                    if (cache.TryGetValue<SchoolTranscriptDto>(key, out var current)) return current;
                    var result = await BuildOverallTranscript(null, academicYear, gradeLevel, token);
                    if (result != null) cache.Set(key, result, AppMemoryCache.Entry(TimeSpan.FromSeconds(30), size: 8));
                    return result;
                }
                snapshot = requests == null ? await Build(cancellationToken)
                    : await requests.RunAsync(key, Build, TimeSpan.FromSeconds(15), cancellationToken);
            }
        }
        if (snapshot == null) return NotFound(new { Message = "Student was not found." });
        cancellationToken.ThrowIfCancellationRequested();
        return Ok(ProjectSnapshot(snapshot, excludedSubjects, subjectPage, subjectPageSize));
    }

    private static SchoolTranscriptDto ProjectSnapshot(SchoolTranscriptDto source, string[]? excludedSubjects,
        int? subjectPage, int? subjectPageSize)
    {
        var excluded = ExcludedSubjectCodes(excludedSubjects);
        var paginate = subjectPage.HasValue || subjectPageSize.HasValue;
        var page = Math.Max(subjectPage ?? 1, 1);
        var size = Math.Clamp(subjectPageSize ?? 10, 5, 50);
        return new SchoolTranscriptDto
        {
            Record = source.Record, CanEditRecord = source.CanEditRecord, TranscriptName = source.TranscriptName,
            AcademicYearOptions = source.AcademicYearOptions, StudentProfileId = source.StudentProfileId,
            SISUserId = source.SISUserId, StudentName = source.StudentName, DateOfBirth = source.DateOfBirth,
            GradeLevel = source.GradeLevel, Homeroom = source.Homeroom, Gender = source.Gender,
            OverallAverage = source.OverallAverage, OverallGpa = source.OverallGpa,
            TotalAcademicYears = source.TotalAcademicYears, TotalStudents = source.TotalStudents,
            TotalSubjects = source.SubjectOptions.Count(subject => !excluded.Contains(subject.SubjectCode)),
            SubjectOptions = source.SubjectOptions,
            Years = source.Years.Select(year => new SchoolTranscriptYearDto
            {
                AcademicYear = year.AcademicYear, ShowAverage = year.ShowAverage, AverageScore = year.AverageScore,
                Grades = year.Grades.Select(grade =>
                {
                    var subjects = grade.Subjects.Where(subject => !excluded.Contains(subject.SubjectCode)).ToList();
                    return new SchoolTranscriptGradeDto
                    {
                        GradeLevel = grade.GradeLevel, AverageScore = grade.AverageScore, Gpa = grade.Gpa,
                        ClassOptions = grade.ClassOptions, TotalSubjects = subjects.Count,
                        SubjectPage = paginate ? page : 1, SubjectPageSize = paginate ? size : subjects.Count,
                        TotalSubjectPages = subjects.Count == 0 ? 0 : paginate ? (int)Math.Ceiling(subjects.Count / (double)size) : 1,
                        Subjects = paginate ? subjects.Skip((page - 1) * size).Take(size).ToList() : subjects
                    };
                }).ToList()
            }).ToList()
        };
    }

    private async Task<SchoolTranscriptDto?> BuildOverallTranscript(int? studentId, string? academicYear, string? gradeLevel, CancellationToken cancellationToken)
    {
        StudentProfile? selectedStudent = null;
        if (studentId != null)
        {
            selectedStudent = await VisibleStudents()
                .Include(student => student.HealthVisits)
                .FirstOrDefaultAsync(student => student.Id == studentId.Value, cancellationToken);
            if (selectedStudent == null)
                return null;
        }

        var visibleStudents = VisibleStudents();
        var recordRows = await context.StudentTranscriptRecords.AsNoTracking()
            .Where(row => (studentId == null || row.StudentProfileId == studentId) &&
                visibleStudents.Any(student => student.Id == row.StudentProfileId)).ToListAsync(cancellationToken);
        var authored = recordRows.ToDictionary(row => row.StudentProfileId, ReadRecord);
        var selectedRecord = studentId.HasValue && authored.TryGetValue(studentId.Value, out var savedRecord)
            ? savedRecord : new TranscriptRecordDto();
        var scores = userAccess.ApplyStudentScope(context.CanvasCourseScores.AsNoTracking(), score => score.Student!)
            .Where(score =>
                (score.GradingPeriodStartDate == null || score.GradingPeriodStartDate <= DateTime.UtcNow) &&
                score.Enrollment!.Course!.EnrollmentTermName != null &&
                score.Enrollment.Course.EnrollmentTermName != "" &&
                score.Student != null &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null));
        if (studentId != null)
            scores = scores.Where(score => score.StudentProfileId == studentId.Value);
        if (academicYear != null || gradeLevel != null)
        {
            // Resolve the bounded course IDs from directory metadata before touching marks.
            // Use the codebase's explicit predicates: this MySQL provider cannot reliably map
            // captured IN collections inside the qualified-score UNION.
            var directory = await context.CanvasCourses.AsNoTracking().Select(course => new {
                course.CanvasCourseId, course.Name, course.CourseCode, course.CanvasGradeLevel,
                course.EnrollmentTermName }).ToListAsync(cancellationToken);
            if (academicYear != null)
            {
                var terms = directory.Select(course => course.EnrollmentTermName).OfType<string>().Distinct().ToArray();
                var aliases = terms.Where(term => DashboardPeriodNormalizer.AcademicYear(term) == academicYear).Append(academicYear);
                var otherYears = terms.Where(term => DashboardPeriodNormalizer.IsAcademicYear(term) && DashboardPeriodNormalizer.AcademicYear(term) != academicYear);
                var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
                scores = scores.WhereMatchesNone(score => score.Enrollment!.Course!.EnrollmentTermName, otherYears)
                    .WhereMatchesAny(score => score.Enrollment!.Course!.EnrollmentTermName, aliases,
                        score => score.GradingPeriodEndDate >= start && score.GradingPeriodEndDate < end);
            }
            if (gradeLevel != null)
            {
                var ids = directory.Where(course => TranscriptRules.HistoricalGrade(course.Name, course.CourseCode, course.CanvasGradeLevel) == gradeLevel)
                    .Select(course => course.CanvasCourseId).ToArray();
                var overridden = authored.Where(pair => pair.Value.Years.Any(year => year.AcademicYear == academicYear && !string.IsNullOrWhiteSpace(year.GradeLevel))).ToList();
                var overriddenIds = overridden.Select(pair => pair.Key).ToArray();
                var matchingIds = overridden.Where(pair => pair.Value.Years.Any(year => year.AcademicYear == academicYear && year.GradeLevel == gradeLevel)).Select(pair => pair.Key).ToArray();
                var matching = scores.WhereMatchesAnyValue(score => score.Enrollment!.CanvasCourseId, ids)
                    .WhereMatchesNoneValue(score => score.StudentProfileId!.Value, overriddenIds);
                if (matchingIds.Length > 0)
                    matching = matching.Concat(scores.WhereMatchesAnyValue(score => score.StudentProfileId!.Value, matchingIds));
                scores = matching;
            }
        }
        var availableYears = studentId.HasValue ? (await scores.Select(score => new {
            Year = score.Enrollment!.Course!.EnrollmentTermName!, End = score.GradingPeriodEndDate }).Distinct().ToListAsync(cancellationToken))
            .Select(row => DashboardPeriodNormalizer.AcademicYear(row.Year, row.End)).Where(DashboardPeriodNormalizer.IsAcademicYear).Distinct().ToList() : [];
        scores = WithAssessmentEvidence(scores);

        var overrideStudents = authored.Where(pair => pair.Value.Years.Any(year => !string.IsNullOrWhiteSpace(year.GradeLevel)))
            .Select(pair => pair.Key).ToArray();
        var resolutions = new Dictionary<long, (TranscriptSubjectResolution Subject, string? Grade, string Class)>();
        var groupedRows = await scores
            .GroupBy(score => new
            {
                AcademicYear = score.Enrollment!.Course!.EnrollmentTermName!,
                StudentProfileId = studentId.HasValue || overrideStudents.Length > 0
                    ? score.StudentProfileId : null,
                score.GradingPeriodEndDate,
                GradeLevel = score.Enrollment!.Course!.CanvasGradeLevel,
                score.Enrollment.Course.EndAt,
                score.Enrollment.Course.CanvasCourseId,
                score.Enrollment.Course.SisCourseId,
                score.Enrollment.Course.CourseCode,
                CourseName = score.Enrollment.Course.Name ?? score.Enrollment.Course.CourseCode ?? "—",
                Term = score.GradingPeriodTitle ?? score.Enrollment.Course.EnrollmentTermName!
            })
            .Select(group => new
            {
                group.Key.AcademicYear,
                group.Key.StudentProfileId,
                group.Key.GradingPeriodEndDate,
                group.Key.GradeLevel,
                group.Key.EndAt,
                group.Key.CanvasCourseId,
                group.Key.SisCourseId,
                group.Key.CourseCode,
                group.Key.CourseName,
                group.Key.Term,
                // Canvas current_score excludes ungraded assignments, while final_score treats
                // them as zero. For a live transcript, prefer the meaningful current result.
                Average = group.Average(score => score.EnrollmentOverrideScore ?? score.GradesOverrideScore ??
                    score.CurrentScore ?? (score.FinalScore == 0 && score.CurrentGrade == null &&
                        score.FinalGrade == null ? null : score.FinalScore)),
                RecordCount = group.Count(score => score.EnrollmentOverrideScore != null ||
                    score.GradesOverrideScore != null || score.CurrentScore != null ||
                    score.FinalScore != null && (score.FinalScore != 0 ||
                        score.CurrentGrade != null || score.FinalGrade != null))
            })
            .ToListAsync(cancellationToken);

        var rows = groupedRows.Select(row =>
        {
            if (!resolutions.TryGetValue(row.CanvasCourseId, out var resolution))
            {
                resolution = (subjectResolver.Resolve(row.CanvasCourseId, row.SisCourseId, row.CourseCode, row.CourseName),
                    TranscriptRules.HistoricalGrade(row.CourseName, row.CourseCode, row.GradeLevel),
                    ExtractClassName(row.CourseName, row.CourseCode));
                resolutions.Add(row.CanvasCourseId, resolution);
            }
            var subject = resolution.Subject;
            var rowYear = DashboardPeriodNormalizer.AcademicYear(row.AcademicYear, row.GradingPeriodEndDate);
            var yearRecord = row.StudentProfileId.HasValue && authored.TryGetValue(row.StudentProfileId.Value, out var personal)
                ? personal.Years.FirstOrDefault(year => year.AcademicYear == rowYear) : null;
            var rowGrade = yearRecord?.GradeLevel ?? resolution.Grade;
            var term = NormalizeTerm(row.Term);
            return new AggregateRow(
                rowYear,
                rowGrade ?? "",
                subject.Code,
                subject.Name,
                row.CanvasCourseId,
                resolution.Class,
                row.CourseName.Trim(),
                term,
                IsYear(term) && !TranscriptRules.AnnualTotalAvailable(rowYear, row.EndAt, DateTime.UtcNow) ? null : row.Average,
                row.RecordCount);
        })
            .Where(row => DashboardPeriodNormalizer.IsAcademicYear(row.AcademicYear) && (academicYear == null || row.AcademicYear == academicYear))
            .ToList();
        var derivedRows = rows.Select(row => new
        {
            row.AcademicYear,
            GradeLevel = NormalizeGrade(row.GradeLevel),
            row.SubjectCode,
            row.Subject,
            row.CanvasCourseId,
            row.ClassName,
            row.CourseName,
            row.Term,
            row.Average,
            row.RecordCount
        }).Where(row => !string.IsNullOrWhiteSpace(row.GradeLevel) && (gradeLevel == null || row.GradeLevel == gradeLevel)).ToList();

        var years = derivedRows
            .GroupBy(row => row.AcademicYear)
            .OrderBy(group => StartYear(group.Key))
            .Select(yearGroup => new SchoolTranscriptYearDto
            {
                AcademicYear = yearGroup.Key,
                ShowAverage = selectedRecord.Years.FirstOrDefault(year => year.AcademicYear == yearGroup.Key)?.ShowAverage
                    ?? StartYear(yearGroup.Key) < 2024,
                Grades = yearGroup
                    .GroupBy(row => row.GradeLevel!)
                    .OrderBy(group => int.TryParse(group.Key, out var grade) ? grade : int.MaxValue)
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(gradeGroup =>
                    {
                        var subjects = gradeGroup.GroupBy(row => row.SubjectCode, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(group => subjectResolver.SortOrder(group.Key))
                            .ThenBy(group => group.First().Subject, StringComparer.OrdinalIgnoreCase)
                            .Select(subjectGroup =>
                            {
                                var classScores = studentId == null
                                    ? subjectGroup
                                        .GroupBy(row => new
                                        {
                                            row.CanvasCourseId,
                                            row.ClassName,
                                            row.CourseName
                                        })
                                        .OrderBy(group => group.Key.ClassName, StringComparer.OrdinalIgnoreCase)
                                        .ThenBy(group => group.Key.CourseName, StringComparer.OrdinalIgnoreCase)
                                        .Select(courseGroup =>
                                        {
                                            var classSemester1 = WeightedAverage(courseGroup
                                                .Where(row => IsSemester(row.Term, 1))
                                                .Select(row => (row.Average, row.RecordCount)));
                                            var classSemester2 = WeightedAverage(courseGroup
                                                .Where(row => IsSemester(row.Term, 2))
                                                .Select(row => (row.Average, row.RecordCount)));
                                            var classYear = WeightedAverage(courseGroup
                                                .Where(row => IsYear(row.Term))
                                                .Select(row => (row.Average, row.RecordCount)));
                                            return new TranscriptClassScoreDto
                                            {
                                                CanvasCourseId = courseGroup.Key.CanvasCourseId,
                                                ClassName = courseGroup.Key.ClassName,
                                                CourseName = courseGroup.Key.CourseName,
                                                Semester1Final = classSemester1,
                                                Semester2Final = classSemester2,
                                                YearAverage = AnnualAverage(classSemester1, classSemester2, classYear),
                                                Credit = CreditForSubject(
                                                    classSemester1, classSemester2, classYear),
                                                RecordCount = courseGroup.Sum(row => row.RecordCount)
                                            };
                                        }).ToList()
                                    : [];
                                var semester1Final = WeightedAverage(subjectGroup
                                    .Where(row => IsSemester(row.Term, 1))
                                    .Select(row => (row.Average, row.RecordCount)));
                                var semester2Final = WeightedAverage(subjectGroup
                                    .Where(row => IsSemester(row.Term, 2))
                                    .Select(row => (row.Average, row.RecordCount)));
                                var explicitYearFinal = WeightedAverage(subjectGroup
                                    .Where(row => IsYear(row.Term))
                                    .Select(row => (row.Average, row.RecordCount)));
                                return new TranscriptSubjectDto
                                {
                                    SubjectCode = subjectGroup.Key,
                                    Subject = subjectGroup.First().Subject,
                                    EnglishName = TranscriptSubjectResolver.DisplayNames(subjectGroup.First().Subject).English,
                                    VietnameseName = TranscriptSubjectResolver.DisplayNames(subjectGroup.First().Subject).Vietnamese,
                                    CourseNames = subjectGroup.Select(row => row.CourseName)
                                        .Where(value => !string.IsNullOrWhiteSpace(value))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                                        .ToList(),
                                    ClassScores = classScores,
                                    Semester1Final = semester1Final,
                                    Semester2Final = semester2Final,
                                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal),
                                    Credit = selectedRecord.Years.FirstOrDefault(year => year.AcademicYear == yearGroup.Key)?
                                        .Credits.FirstOrDefault(credit => credit.SubjectCode.Equals(subjectGroup.Key, StringComparison.OrdinalIgnoreCase))?.Credit ?? 1
                                };
                            }).ToList();
                        foreach (var subject in subjects) {
                            var entry = selectedRecord.Years.FirstOrDefault(year => year.AcademicYear == yearGroup.Key)?
                                .Credits.FirstOrDefault(row => row.SubjectCode.Equals(subject.SubjectCode, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrWhiteSpace(entry?.EnglishName)) subject.EnglishName = TranscriptSubjectResolver.CleanDisplayName(entry.EnglishName);
                            if (!string.IsNullOrWhiteSpace(entry?.VietnameseName)) subject.VietnameseName = TranscriptSubjectResolver.CleanDisplayName(entry.VietnameseName);
                            if (entry?.EnglishName != null || entry?.VietnameseName != null)
                                subject.Subject = $"{subject.VietnameseName}/{subject.EnglishName}";
                        }
                        var gpa = WeightedGpa(subjects);
                        var totalSubjects = subjects.Count;
                        return new SchoolTranscriptGradeDto
                        {
                            GradeLevel = gradeGroup.Key,
                            AverageScore = AnnualAverage(
                                WeightedAverage(gradeGroup.Where(row => IsSemester(row.Term, 1)).Select(row => (row.Average, row.RecordCount))),
                                WeightedAverage(gradeGroup.Where(row => IsSemester(row.Term, 2)).Select(row => (row.Average, row.RecordCount))),
                                WeightedAverage(gradeGroup.Where(row => IsYear(row.Term)).Select(row => (row.Average, row.RecordCount)))),
                            Gpa = gpa,
                            ClassOptions = studentId == null
                                ? gradeGroup.Select(row => row.ClassName)
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                                    .ToList()
                                : [],
                            Subjects = subjects,
                            SubjectPage = 1,
                            SubjectPageSize = totalSubjects,
                            TotalSubjects = totalSubjects,
                            TotalSubjectPages = totalSubjects == 0 ? 0 : 1
                        };
                    }).ToList()
            }).ToList();

        foreach (var year in years)
            year.AverageScore = year.Grades.Any(grade => grade.AverageScore == null) ? null
                : WeightedAverage(year.Grades.Select(grade => (grade.AverageScore, 1)));

        foreach (var authoredYear in selectedRecord.Years.Where(year => !string.IsNullOrWhiteSpace(year.GradeLevel) && year.IbSubjects.Count > 0 && (academicYear == null || year.AcademicYear == academicYear) && (gradeLevel == null || year.GradeLevel == gradeLevel)))
        {
            var year = years.FirstOrDefault(year => year.AcademicYear == authoredYear.AcademicYear);
            if (year == null)
            {
                year = new SchoolTranscriptYearDto { AcademicYear = authoredYear.AcademicYear,
                    ShowAverage = authoredYear.ShowAverage ?? StartYear(authoredYear.AcademicYear) < 2024 };
                years.Add(year);
            }
            if (year.Grades.All(grade => grade.GradeLevel != authoredYear.GradeLevel))
                year.Grades.Add(new SchoolTranscriptGradeDto { GradeLevel = authoredYear.GradeLevel! });
        }
        years = years.OrderBy(year => StartYear(year.AcademicYear)).ToList();

        var usableScores = scores.Where(HasUsableTranscriptScore);
        var totalStudents = studentId != null
            ? await usableScores.Select(score => score.StudentProfileId).Distinct().CountAsync(cancellationToken)
            : await usableScores.Select(score => score.Enrollment!.CanvasUserId).Distinct().CountAsync(cancellationToken);
        return new SchoolTranscriptDto
        {
            Record = selectedRecord,
            CanEditRecord = studentId.HasValue && CanEditRecord,
            TranscriptName = selectedRecord.TranscriptName ?? TranscriptRules.InternationalName(selectedStudent?.FullName),
            AcademicYearOptions = availableYears.Concat(groupedRows.Select(row => DashboardPeriodNormalizer.AcademicYear(row.AcademicYear, row.GradingPeriodEndDate)))
                .Where(DashboardPeriodNormalizer.IsAcademicYear).Concat(selectedRecord.Years.Select(year => year.AcademicYear))
                .Distinct().OrderBy(StartYear).ToList(),
            StudentProfileId = selectedStudent?.Id,
            SISUserId = selectedStudent?.SISUserId,
            StudentName = selectedStudent?.FullName,
            DateOfBirth = selectedStudent?.DateOfBirth,
            GradeLevel = selectedStudent?.GradeLevel ?? selectedStudent?.CanvasGradeLevel ??
                years.LastOrDefault()?.Grades.LastOrDefault()?.GradeLevel,
            Homeroom = selectedStudent?.Homeroom,
            Gender = selectedStudent?.Gender ?? selectedStudent?.HealthVisits
                .Where(visit => !string.IsNullOrWhiteSpace(visit.Gender))
                .OrderByDescending(visit => visit.ExamTime)
                .Select(visit => visit.Gender)
                .FirstOrDefault(),
            OverallAverage = years.Any(year => year.AverageScore == null) ? null
                : WeightedAverage(years.Select(year => (year.AverageScore, 1))),
            // The official Academic GPA is the simple average of the displayed
            // annual GPAs (the spreadsheet's AVERAGE of the Grade 9-12 GPA cells).
            OverallGpa = AcademicGpa(years),
            TotalAcademicYears = years.Count,
            TotalStudents = totalStudents,
            TotalSubjects = derivedRows
                .Select(row => row.SubjectCode)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            SubjectOptions = derivedRows.GroupBy(row => row.SubjectCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => subjectResolver.SortOrder(group.Key))
                .ThenBy(group => group.First().Subject, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TranscriptSubjectOptionDto
                {
                    SubjectCode = group.Key,
                    Subject = group.First().Subject,
                    GradeLevels = group.Select(row => row.GradeLevel!).Distinct().OrderBy(value => int.Parse(value)).ToList()
                }).ToList(),
            Years = years
        };
    }

    [HttpGet("{academicYear}/grades/{gradeLevel}")]
    public async Task<ActionResult<SchoolTranscriptGradeDetailDto>> GetGradeDetails(
        string academicYear,
        string gradeLevel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] int subjectPage = 1,
        [FromQuery] int subjectPageSize = 10,
        [FromQuery] string? subject = null,
        [FromQuery] string? term = null,
        [FromQuery] string? className = null,
        [FromQuery] string[]? excludedSubjects = null,
        CancellationToken cancellationToken = default)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        if (!cancellationToken.CanBeCanceled) cancellationToken = HttpContext?.RequestAborted ?? default;
        var excludedSubjectCodes = ExcludedSubjectCodes(excludedSubjects);
        academicYear = academicYear.Trim();
        gradeLevel = gradeLevel.Trim();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);
        subjectPage = Math.Max(subjectPage, 1);
        subjectPageSize = Math.Clamp(subjectPageSize, 5, 50);
        className = className?.Trim();

        // Canvas term names are descriptive labels (for example,
        // "Năm học 25-26 THCS G6-8"), not the former short SIS academic-year code.
        // Keep this in sync with CanvasCourse.EnrollmentTermName's warehouse length.
        if (academicYear.Length is < 1 or > 100 || gradeLevel.Length is < 1 or > 50 ||
            className?.Length > 50)
            return BadRequest(new { Message = "Invalid academic year or grade level." });

        var academicYearAliases = (await context.CanvasCourses.AsNoTracking()
                .Where(course => course.EnrollmentTermName != null && course.EnrollmentTermName != "")
                .Select(course => course.EnrollmentTermName!)
                .Distinct()
                .ToListAsync(cancellationToken))
            .Where(value => string.Equals(
                DashboardPeriodNormalizer.AcademicYear(value), academicYear,
                StringComparison.OrdinalIgnoreCase))
            .Append(academicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var (academicYearStart, academicYearEnd) =
            DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        var baseQuery = userAccess.ApplyStudentScope(context.CanvasCourseScores.AsNoTracking(), score => score.Student!)
            .WhereMatchesAny(score => score.Enrollment!.Course!.EnrollmentTermName,
                academicYearAliases, score => score.GradingPeriodEndDate >= academicYearStart &&
                    score.GradingPeriodEndDate < academicYearEnd)
            .Where(score => score.StudentProfileId != null && score.Student != null &&
                    (score.GradingPeriodStartDate == null || score.GradingPeriodStartDate <= DateTime.UtcNow) &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null));
        var courseCandidates = await context.CanvasCourses.AsNoTracking().Select(course => new { course.CanvasCourseId,
            course.Name, course.CourseCode, course.CanvasGradeLevel }).ToListAsync(cancellationToken);
        var gradeCourseIds = courseCandidates.Where(course =>
                TranscriptRules.HistoricalGrade(course.Name, course.CourseCode, course.CanvasGradeLevel) == gradeLevel)
            .Select(course => course.CanvasCourseId).ToArray();
        var visibleStudents = VisibleStudents();
        var authoredRows = await context.StudentTranscriptRecords.AsNoTracking()
            .Where(row => visibleStudents.Any(student => student.Id == row.StudentProfileId)).ToListAsync(cancellationToken);
        var gradeOverrides = authoredRows.Select(row => new { row.StudentProfileId,
            Grade = ReadRecord(row).Years.FirstOrDefault(year => year.AcademicYear == academicYear)?.GradeLevel })
            .Where(row => !string.IsNullOrWhiteSpace(row.Grade)).ToList();
        var overriddenStudents = gradeOverrides.Select(row => row.StudentProfileId).ToArray();
        var matchingStudents = gradeOverrides.Where(row => row.Grade == gradeLevel).Select(row => row.StudentProfileId).ToArray();
        baseQuery = baseQuery.Where(score => matchingStudents.Contains(score.StudentProfileId!.Value) ||
            !overriddenStudents.Contains(score.StudentProfileId!.Value) && gradeCourseIds.Contains(score.Enrollment!.CanvasCourseId));
        baseQuery = WithAssessmentEvidence(baseQuery);

        var aggregateRows = await baseQuery
            .GroupBy(score => new
            {
                score.Enrollment!.Course!.CanvasCourseId,
                score.Enrollment.Course.SisCourseId,
                score.Enrollment.Course.CourseCode,
                CourseName = score.Enrollment.Course.Name ?? score.Enrollment.Course.CourseCode ?? "—",
                score.Enrollment.Course.EndAt,
                Term = score.GradingPeriodTitle ?? score.Enrollment.Course.EnrollmentTermName ?? "—"
            })
            .Select(group => new
            {
                group.Key.CanvasCourseId,
                group.Key.SisCourseId,
                group.Key.CourseCode,
                group.Key.CourseName,
                group.Key.EndAt,
                group.Key.Term,
                Average = group.Average(score => score.EnrollmentOverrideScore ?? score.GradesOverrideScore ??
                    score.CurrentScore ?? (score.FinalScore == 0 && score.CurrentGrade == null &&
                        score.FinalGrade == null ? null : score.FinalScore)),
                RecordCount = group.Count(score => score.EnrollmentOverrideScore != null ||
                    score.GradesOverrideScore != null || score.CurrentScore != null ||
                    score.FinalScore != null && (score.FinalScore != 0 ||
                        score.CurrentGrade != null || score.FinalGrade != null))
            })
            .ToListAsync(cancellationToken);

        var normalizedAggregateRows = aggregateRows.Select(row =>
        {
            var subject = subjectResolver.Resolve(
                row.CanvasCourseId, row.SisCourseId, row.CourseCode, row.CourseName);
            return new
            {
                row.CanvasCourseId,
                SubjectCode = subject.Code,
                Subject = subject.Name,
                ClassName = ExtractClassName(row.CourseName, row.CourseCode),
                CourseName = row.CourseName.Trim(),
                Term = NormalizeTerm(row.Term),
                Average = IsYear(NormalizeTerm(row.Term)) && !TranscriptRules.AnnualTotalAvailable(academicYear, row.EndAt, DateTime.UtcNow) ? null : row.Average,
                row.RecordCount
            };
        }).ToList();
        var classOptions = normalizedAggregateRows.Select(row => row.ClassName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        long[] selectedClassCourseIds = string.IsNullOrWhiteSpace(className)
            ? []
            : normalizedAggregateRows
                .Where(row => string.Equals(row.ClassName, className, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.CanvasCourseId)
                .Distinct()
                .ToArray();
        var classFilteredRows = string.IsNullOrWhiteSpace(className)
            ? normalizedAggregateRows
            : normalizedAggregateRows
                .Where(row => selectedClassCourseIds.Contains(row.CanvasCourseId))
                .ToList();
        var subjects = classFilteredRows
            .GroupBy(row => row.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => subjectResolver.SortOrder(group.Key))
            .ThenBy(group => group.First().Subject, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var classScores = group
                    .GroupBy(row => new { row.CanvasCourseId, row.ClassName, row.CourseName })
                    .OrderBy(classGroup => classGroup.Key.ClassName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(classGroup => classGroup.Key.CourseName, StringComparer.OrdinalIgnoreCase)
                    .Select(classGroup =>
                    {
                        var classSemester1 = WeightedAverage(classGroup
                            .Where(row => IsSemester(row.Term, 1))
                            .Select(row => (row.Average, row.RecordCount)));
                        var classSemester2 = WeightedAverage(classGroup
                            .Where(row => IsSemester(row.Term, 2))
                            .Select(row => (row.Average, row.RecordCount)));
                        var classYear = WeightedAverage(classGroup
                            .Where(row => IsYear(row.Term))
                            .Select(row => (row.Average, row.RecordCount)));
                        return new TranscriptClassScoreDto
                        {
                            CanvasCourseId = classGroup.Key.CanvasCourseId,
                            ClassName = classGroup.Key.ClassName,
                            CourseName = classGroup.Key.CourseName,
                            Semester1Final = classSemester1,
                            Semester2Final = classSemester2,
                            YearAverage = AnnualAverage(classSemester1, classSemester2, classYear),
                            Credit = CreditForSubject(classSemester1, classSemester2, classYear),
                            RecordCount = classGroup.Sum(row => row.RecordCount)
                        };
                    }).ToList();
                var semester1Final = WeightedAverage(group.Where(row => IsSemester(row.Term, 1))
                    .Select(row => (row.Average, row.RecordCount)));
                var semester2Final = WeightedAverage(group.Where(row => IsSemester(row.Term, 2))
                    .Select(row => (row.Average, row.RecordCount)));
                var explicitYearFinal = WeightedAverage(group.Where(row => IsYear(row.Term))
                    .Select(row => (row.Average, row.RecordCount)));

                return new TranscriptSubjectDto
                {
                    SubjectCode = group.Key,
                    Subject = group.First().Subject,
                    EnglishName = TranscriptSubjectResolver.DisplayNames(group.First().Subject).English,
                    VietnameseName = TranscriptSubjectResolver.DisplayNames(group.First().Subject).Vietnamese,
                    CourseNames = group.Select(row => row.CourseName)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    ClassScores = classScores,
                    Semester1Final = semester1Final,
                    Semester2Final = semester2Final,
                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal),
                    Credit = CreditForSubject(semester1Final, semester2Final, explicitYearFinal)
                };
            }).ToList();

        var classFilteredQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(className))
        {
            classFilteredQuery = selectedClassCourseIds.Length == 0
                ? classFilteredQuery.Where(_ => false)
                : classFilteredQuery.Where(score =>
                    selectedClassCourseIds.Contains(score.Enrollment!.CanvasCourseId));
        }

        var recordsQuery = classFilteredQuery
            .Where(score => score.StudentProfileId != null && score.Student != null);
        var excludedCourseIds = classFilteredRows
            .Where(row => excludedSubjectCodes.Contains(row.SubjectCode))
            .Select(row => row.CanvasCourseId)
            .Distinct()
            .ToArray();
        if (excludedCourseIds.Length > 0)
            recordsQuery = recordsQuery.Where(score => !excludedCourseIds.Contains(score.Enrollment!.CanvasCourseId));
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var requestedSubject = subject.Trim();
            var matchingCourseIds = classFilteredRows
                .Where(row =>
                    string.Equals(row.SubjectCode, requestedSubject, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.Subject, requestedSubject, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.CourseName, requestedSubject, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.CanvasCourseId)
                .Distinct()
                .ToArray();
            recordsQuery = matchingCourseIds.Length == 0
                ? recordsQuery.Where(_ => false)
                : recordsQuery.Where(score => matchingCourseIds.Contains(score.Enrollment!.CanvasCourseId));
        }
        if (!string.IsNullOrWhiteSpace(term))
        {
            var normalizedTerm = DashboardPeriodNormalizer.Term(term);
            var termAliases = (await context.CanvasCourseScores.AsNoTracking()
                    .Where(score => score.GradingPeriodTitle != null)
                    .Select(score => score.GradingPeriodTitle!)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .Where(value => DashboardPeriodNormalizer.Term(value)
                    .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                .Append(term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            recordsQuery = recordsQuery.WhereMatchesAny(score => score.GradingPeriodTitle, termAliases);
        }

        var totalRecords = await recordsQuery.CountAsync(cancellationToken);
        var rawRecords = await recordsQuery
            .OrderBy(score => score.Enrollment!.Course!.Name ?? score.Enrollment.Course.CourseCode)
            .ThenBy(score => score.Student!.FullName)
            .ThenBy(score => score.GradingPeriodTitle ?? score.Enrollment!.Course!.EnrollmentTermName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(score => new
            {
                score.WarehouseScoreFactId,
                score.CanvasEnrollmentId,
                score.Enrollment!.CanvasUserId,
                score.Enrollment.CanvasCourseId,
                StudentProfileId = score.StudentProfileId!.Value,
                score.Student!.SISUserId,
                StudentName = score.Student.FullName,
                CourseName = score.Enrollment.Course!.Name ?? score.Enrollment.Course.CourseCode ?? "—",
                CourseCode = score.Enrollment.Course!.CourseCode,
                EnrollmentTermId = score.Enrollment.Course!.EnrollmentTermId,
                EnrollmentTermName = score.Enrollment.Course!.EnrollmentTermName,
                score.GradingPeriodId,
                score.GradingPeriodTitle,
                Score = score.EnrollmentOverrideScore ?? score.GradesOverrideScore ??
                    score.CurrentScore ?? (score.FinalScore == 0 && score.CurrentGrade == null &&
                        score.FinalGrade == null ? null : score.FinalScore),
                ScoreSource = score.EnrollmentOverrideScore != null ? "enrollment_override" :
                    score.GradesOverrideScore != null ? "grades_override" :
                    score.CurrentScore != null ? "current" :
                    score.FinalScore == 0 && score.CurrentGrade == null && score.FinalGrade == null
                        ? "unavailable" : "final"
            })
            .ToListAsync(cancellationToken);
        var records = rawRecords.Select(score => new TranscriptScoreRecordDto
        {
            WarehouseScoreFactId = score.WarehouseScoreFactId,
            CanvasEnrollmentId = score.CanvasEnrollmentId,
            CanvasUserId = score.CanvasUserId,
            CanvasCourseId = score.CanvasCourseId,
            StudentProfileId = score.StudentProfileId,
            SISUserId = score.SISUserId,
            StudentName = score.StudentName,
            CourseName = score.CourseName,
            CourseCode = score.CourseCode,
            EnrollmentTermId = score.EnrollmentTermId,
            EnrollmentTermName = score.EnrollmentTermName,
            GradingPeriodId = score.GradingPeriodId,
            GradingPeriodTitle = score.GradingPeriodTitle,
            ScoreScope = score.GradingPeriodId == null ? "course_total" : "grading_period",
            Score = score.Score,
            ScoreSource = score.ScoreSource
        }).ToList();

        var totalStudents = await classFilteredQuery.Where(HasUsableTranscriptScore)
            .Select(score => score.Enrollment!.CanvasUserId)
            .Distinct().CountAsync(cancellationToken);
        var allRecordCount = classFilteredRows.Sum(row => row.RecordCount);
        var gpa = WeightedGpa(subjects);
        subjects = subjects.Where(subject => !excludedSubjectCodes.Contains(subject.SubjectCode)).ToList();
        var totalSubjects = subjects.Count;
        return Ok(new SchoolTranscriptGradeDetailDto
        {
            AcademicYear = academicYear,
            GradeLevel = gradeLevel,
            AverageScore = AnnualAverage(
                WeightedAverage(classFilteredRows.Where(row => IsSemester(row.Term, 1)).Select(row => (row.Average, row.RecordCount))),
                WeightedAverage(classFilteredRows.Where(row => IsSemester(row.Term, 2)).Select(row => (row.Average, row.RecordCount))),
                WeightedAverage(classFilteredRows.Where(row => IsYear(row.Term)).Select(row => (row.Average, row.RecordCount)))),
            Gpa = gpa,
            TotalStudents = totalStudents,
            TotalSubjects = totalSubjects,
            TotalRecords = allRecordCount,
            MatchingRecords = totalRecords,
            ClassOptions = classOptions,
            SubjectOptions = subjects.Select(item => new TranscriptSubjectOptionDto
            {
                SubjectCode = item.SubjectCode,
                Subject = item.Subject
            }).ToList(),
            Subjects = subjects.Skip((subjectPage - 1) * subjectPageSize).Take(subjectPageSize).ToList(),
            SubjectPage = subjectPage,
            SubjectPageSize = subjectPageSize,
            TotalSubjectPages = totalSubjects == 0
                ? 0
                : (int)Math.Ceiling(totalSubjects / (double)subjectPageSize),
            Records = records,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize)
        });
    }

    private static HashSet<string> ExcludedSubjectCodes(string[]? subjects) =>
        (subjects ?? []).Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Select(subject => subject.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private IQueryable<StudentProfile> VisibleStudents()
        => userAccess.ApplyStudentScope(context.StudentProfiles.AsNoTracking());

    private static string? NormalizeGrade(string gradeLevel)
    {
        if (!int.TryParse(gradeLevel, out var grade)) return gradeLevel;
        return grade is >= 1 and <= 12 ? grade.ToString() : null;
    }

    private static int StartYear(string value) =>
        int.TryParse(value.Split('-', '/', StringSplitOptions.TrimEntries)[0], out var year) ? year : 0;

    private static string ExtractClassName(string? courseName, string? courseCode)
    {
        foreach (var value in new[] { courseName, courseCode })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var matches = ClassNamePattern.Matches(value);
            if (matches.Count > 0) return matches[^1].Groups["class"].Value.ToUpperInvariant();
        }

        return "";
    }

    private static bool IsSemester(string value, int semester)
        => DashboardPeriodNormalizer.Term(value)
            .Equals($"hk{semester}", StringComparison.OrdinalIgnoreCase);

    private static bool IsYear(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "all" or "cả năm" or "year" or "full year" or "yr" or "year final";
    }

    private static string NormalizeTerm(string value)
    {
        var normalized = value.Trim();
        if (IsYear(normalized) || DashboardPeriodNormalizer.IsAcademicYear(normalized)) return "YR";
        var canonical = DashboardPeriodNormalizer.Term(normalized);
        return canonical is "hk1" or "hk2" ? canonical : normalized;
    }

    private static double? AnnualAverage(double? semester1, double? semester2, double? explicitYear) =>
        TranscriptRules.AnnualAverage(semester1, semester2, explicitYear);

    private static double CreditForSubject(double? semester1, double? semester2, double? explicitYear) => 1;

    private static double? WeightedGpa(IEnumerable<TranscriptSubjectDto> subjects) => TranscriptRules.AnnualGpa(subjects);

    private static double? AcademicGpa(IEnumerable<SchoolTranscriptYearDto> years)
    {
        var annualGpas = years
            .SelectMany(year => year.Grades)
            .Where(grade => int.TryParse(grade.GradeLevel, out var gradeLevel) && gradeLevel is >= 9 and <= 12)
            .Where(grade => grade.Gpa != null)
            .Select(grade => grade.Gpa!.Value)
            .ToList();
        return annualGpas.Count == 0
            ? null
            : Math.Round(annualGpas.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static double? WeightedAverage(IEnumerable<(double? Average, int Count)> values)
    {
        var rows = values.Where(value => value.Average != null && value.Count > 0).ToList();
        if (rows.Count == 0) return null;
        var count = rows.Sum(value => value.Count);
        return Math.Round(rows.Sum(value => value.Average!.Value * value.Count) / count, 1,
            MidpointRounding.AwayFromZero);
    }
}
