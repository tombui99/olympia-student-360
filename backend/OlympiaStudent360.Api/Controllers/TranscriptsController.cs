using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
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
    TranscriptSubjectResolver subjectResolver) : ControllerBase
{
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

    [HttpGet("overall")]
    public async Task<ActionResult<SchoolTranscriptDto>> GetOverallTranscript(
        [FromQuery] int? studentId = null,
        [FromQuery] int? subjectPage = null,
        [FromQuery] int? subjectPageSize = null,
        [FromQuery] string[]? excludedSubjects = null)
    {
        var excludedSubjectCodes = ExcludedSubjectCodes(excludedSubjects);
        var paginateSubjects = subjectPage.HasValue || subjectPageSize.HasValue;
        var resolvedSubjectPage = Math.Max(subjectPage ?? 1, 1);
        var resolvedSubjectPageSize = Math.Clamp(subjectPageSize ?? 10, 5, 50);

        StudentProfile? selectedStudent = null;
        if (studentId != null)
        {
            selectedStudent = await VisibleStudents()
                .Include(student => student.HealthVisits)
                .FirstOrDefaultAsync(student => student.Id == studentId.Value);
            if (selectedStudent == null)
                return NotFound(new { Message = "Student was not found." });
        }

        var scores = context.CanvasCourseScores.AsNoTracking()
            .Where(score =>
                (score.GradingPeriodStartDate == null || score.GradingPeriodStartDate <= DateTime.UtcNow) &&
                score.Enrollment!.Course!.EnrollmentTermName != null &&
                score.Enrollment.Course.EnrollmentTermName != "" &&
                score.Student != null &&
                (score.Enrollment.Course.CanvasGradeLevel != null && score.Enrollment.Course.CanvasGradeLevel != "" ||
                    (score.Student.GradeLevel ?? score.Student.CanvasGradeLevel) != null &&
                    (score.Student.GradeLevel ?? score.Student.CanvasGradeLevel) != "") &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null));
        if (studentId != null)
            scores = scores.Where(score => score.StudentProfileId == studentId.Value);

        var groupedRows = await scores
            .GroupBy(score => new
            {
                AcademicYear = score.Enrollment!.Course!.EnrollmentTermName!,
                score.GradingPeriodEndDate,
                GradeLevel = score.Enrollment!.Course!.CanvasGradeLevel ??
                    score.Student!.GradeLevel ?? score.Student.CanvasGradeLevel!,
                score.Enrollment.Course.CanvasCourseId,
                score.Enrollment.Course.SisCourseId,
                score.Enrollment.Course.CourseCode,
                CourseName = score.Enrollment.Course.Name ?? score.Enrollment.Course.CourseCode ?? "—",
                Term = score.GradingPeriodTitle ?? score.Enrollment.Course.EnrollmentTermName!
            })
            .Select(group => new
            {
                group.Key.AcademicYear,
                group.Key.GradingPeriodEndDate,
                group.Key.GradeLevel,
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
            .ToListAsync();

        var rows = groupedRows.Select(row =>
        {
            var subject = subjectResolver.Resolve(
                row.CanvasCourseId, row.SisCourseId, row.CourseCode, row.CourseName);
            return new AggregateRow(
                DashboardPeriodNormalizer.AcademicYear(row.AcademicYear, row.GradingPeriodEndDate),
                row.GradeLevel,
                subject.Code,
                subject.Name,
                row.CanvasCourseId,
                ExtractClassName(row.CourseName, row.CourseCode),
                row.CourseName.Trim(),
                NormalizeTerm(row.Term),
                row.Average,
                row.RecordCount);
        })
            .Where(row => DashboardPeriodNormalizer.IsAcademicYear(row.AcademicYear))
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
        }).Where(row => row.GradeLevel != null).ToList();

        var years = derivedRows
            .GroupBy(row => row.AcademicYear)
            .OrderBy(group => StartYear(group.Key))
            .Select(yearGroup => new SchoolTranscriptYearDto
            {
                AcademicYear = yearGroup.Key,
                AverageScore = WeightedAverage(yearGroup.Select(row => (row.Average, row.RecordCount))),
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
                                                YearAverage = AnnualAverage(
                                                    classSemester1, classSemester2, classYear)
                                                    ?? WeightedAverage(courseGroup.Select(row =>
                                                        (row.Average, row.RecordCount))),
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
                                    CourseNames = subjectGroup.Select(row => row.CourseName)
                                        .Where(value => !string.IsNullOrWhiteSpace(value))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                                        .ToList(),
                                    ClassScores = classScores,
                                    Semester1Final = semester1Final,
                                    Semester2Final = semester2Final,
                                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal)
                                        ?? WeightedAverage(subjectGroup.Select(row => (row.Average, row.RecordCount))),
                                    Credit = CreditForSubject(semester1Final, semester2Final, explicitYearFinal)
                                };
                            }).ToList();
                        var gpa = WeightedGpa(subjects);
                        subjects = subjects.Where(subject => !excludedSubjectCodes.Contains(subject.SubjectCode)).ToList();
                        var totalSubjects = subjects.Count;
                        var totalSubjectPages = totalSubjects == 0
                            ? 0
                            : (int)Math.Ceiling(totalSubjects / (double)resolvedSubjectPageSize);
                        return new SchoolTranscriptGradeDto
                        {
                            GradeLevel = gradeGroup.Key,
                            AverageScore = WeightedAverage(gradeGroup.Select(row => (row.Average, row.RecordCount))),
                            Gpa = gpa,
                            ClassOptions = studentId == null
                                ? gradeGroup.Select(row => row.ClassName)
                                    .Where(value => !string.IsNullOrWhiteSpace(value))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                                    .ToList()
                                : [],
                            Subjects = paginateSubjects
                                ? subjects.Skip((resolvedSubjectPage - 1) * resolvedSubjectPageSize)
                                    .Take(resolvedSubjectPageSize).ToList()
                                : subjects,
                            SubjectPage = paginateSubjects ? resolvedSubjectPage : 1,
                            SubjectPageSize = paginateSubjects ? resolvedSubjectPageSize : totalSubjects,
                            TotalSubjects = totalSubjects,
                            TotalSubjectPages = paginateSubjects ? totalSubjectPages : totalSubjects == 0 ? 0 : 1
                        };
                    }).ToList()
            }).ToList();

        var usableScores = scores.Where(HasUsableTranscriptScore);
        var totalStudents = studentId != null
            ? await usableScores.Select(score => score.StudentProfileId).Distinct().CountAsync()
            : await usableScores.Select(score => score.Enrollment!.CanvasUserId).Distinct().CountAsync();
        return Ok(new SchoolTranscriptDto
        {
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
            OverallAverage = WeightedAverage(derivedRows.Select(row => (row.Average, row.RecordCount))),
            // The official Academic GPA is the simple average of the displayed
            // annual GPAs (the spreadsheet's AVERAGE of the Grade 9-12 GPA cells).
            OverallGpa = AcademicGpa(years),
            TotalAcademicYears = years.Count,
            TotalStudents = totalStudents,
            TotalSubjects = derivedRows.Where(row => !excludedSubjectCodes.Contains(row.SubjectCode))
                .Select(row => row.SubjectCode)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            SubjectOptions = derivedRows.GroupBy(row => row.SubjectCode, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => subjectResolver.SortOrder(group.Key))
                .ThenBy(group => group.First().Subject, StringComparer.OrdinalIgnoreCase)
                .Select(group => new TranscriptSubjectOptionDto
                {
                    SubjectCode = group.Key,
                    Subject = group.First().Subject
                }).ToList(),
            Years = years
        });
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
        [FromQuery] string[]? excludedSubjects = null)
    {
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
                .ToListAsync())
            .Where(value => string.Equals(
                DashboardPeriodNormalizer.AcademicYear(value), academicYear,
                StringComparison.OrdinalIgnoreCase))
            .Append(academicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var (academicYearStart, academicYearEnd) =
            DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        var baseQuery = context.CanvasCourseScores.AsNoTracking()
            .WhereMatchesAny(score => score.Enrollment!.Course!.EnrollmentTermName,
                academicYearAliases, score => score.GradingPeriodEndDate >= academicYearStart &&
                    score.GradingPeriodEndDate < academicYearEnd)
            .Where(score => score.StudentProfileId != null && score.Student != null &&
                    (score.GradingPeriodStartDate == null || score.GradingPeriodStartDate <= DateTime.UtcNow) &&
                    (score.Enrollment!.Course!.CanvasGradeLevel ?? score.Student.GradeLevel ??
                        score.Student.CanvasGradeLevel) == gradeLevel &&
                (score.EnrollmentOverrideScore != null || score.GradesOverrideScore != null ||
                    score.CurrentScore != null || score.FinalScore != null));

        var aggregateRows = await baseQuery
            .GroupBy(score => new
            {
                score.Enrollment!.Course!.CanvasCourseId,
                score.Enrollment.Course.SisCourseId,
                score.Enrollment.Course.CourseCode,
                CourseName = score.Enrollment.Course.Name ?? score.Enrollment.Course.CourseCode ?? "—",
                Term = score.GradingPeriodTitle ?? score.Enrollment.Course.EnrollmentTermName ?? "—"
            })
            .Select(group => new
            {
                group.Key.CanvasCourseId,
                group.Key.SisCourseId,
                group.Key.CourseCode,
                group.Key.CourseName,
                group.Key.Term,
                Average = group.Average(score => score.EnrollmentOverrideScore ?? score.GradesOverrideScore ??
                    score.CurrentScore ?? (score.FinalScore == 0 && score.CurrentGrade == null &&
                        score.FinalGrade == null ? null : score.FinalScore)),
                RecordCount = group.Count(score => score.EnrollmentOverrideScore != null ||
                    score.GradesOverrideScore != null || score.CurrentScore != null ||
                    score.FinalScore != null && (score.FinalScore != 0 ||
                        score.CurrentGrade != null || score.FinalGrade != null))
            })
            .ToListAsync();

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
                row.Average,
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
                            YearAverage = AnnualAverage(classSemester1, classSemester2, classYear)
                                ?? WeightedAverage(classGroup.Select(row =>
                                    (row.Average, row.RecordCount))),
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
                    CourseNames = group.Select(row => row.CourseName)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    ClassScores = classScores,
                    Semester1Final = semester1Final,
                    Semester2Final = semester2Final,
                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal)
                        ?? WeightedAverage(group.Select(row => (row.Average, row.RecordCount))),
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
                    .ToListAsync())
                .Where(value => DashboardPeriodNormalizer.Term(value)
                    .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase))
                .Append(term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            recordsQuery = recordsQuery.WhereMatchesAny(score => score.GradingPeriodTitle, termAliases);
        }

        var totalRecords = await recordsQuery.CountAsync();
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
            .ToListAsync();
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
            .Distinct().CountAsync();
        var allRecordCount = classFilteredRows.Sum(row => row.RecordCount);
        var gpa = WeightedGpa(subjects);
        subjects = subjects.Where(subject => !excludedSubjectCodes.Contains(subject.SubjectCode)).ToList();
        var totalSubjects = subjects.Count;
        return Ok(new SchoolTranscriptGradeDetailDto
        {
            AcademicYear = academicYear,
            GradeLevel = gradeLevel,
            AverageScore = WeightedAverage(classFilteredRows.Select(row => (row.Average, row.RecordCount))),
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

    private static double? AverageAvailable(params double?[] values)
    {
        var available = values.Where(value => value != null).Select(value => value!.Value).ToList();
        return available.Count == 0
            ? null
            : Math.Round(available.Average(), 1, MidpointRounding.AwayFromZero);
    }

    private static double? AnnualAverage(double? semester1, double? semester2, double? explicitYear)
    {
        if (semester1 != null && semester2 != null)
            return Math.Round((semester1.Value + 2 * semester2.Value) / 3, 1,
                MidpointRounding.AwayFromZero);

        return explicitYear ?? AverageAvailable(semester1, semester2);
    }

    private static double CreditForSubject(double? semester1, double? semester2, double? explicitYear)
    {
        // A one-semester subject earns half a credit. A subject represented in
        // both semesters (or only by an explicit full-year result) earns one.
        if ((semester1 != null) != (semester2 != null)) return 0.5;
        return explicitYear != null || semester1 != null || semester2 != null ? 1 : 0;
    }

    private static double? WeightedGpa(IEnumerable<TranscriptSubjectDto> subjects)
    {
        var values = subjects
            .Where(subject => subject.YearAverage != null && subject.Credit > 0)
            .Select(subject => (Gpa: ToGpa(subject.YearAverage!.Value), subject.Credit))
            .ToList();
        if (values.Count == 0) return null;
        return Math.Round(values.Sum(value => value.Gpa * value.Credit) /
            values.Sum(value => value.Credit), 2, MidpointRounding.AwayFromZero);
    }

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

    private static double ToGpa(double score) =>
        score >= 95 ? 4.2 :
        score >= 90 ? 4.0 :
        score >= 80 ? 3.7 :
        score >= 75 ? 3.3 :
        score >= 70 ? 3.0 :
        score >= 65 ? 2.7 :
        score >= 60 ? 2.3 :
        score >= 55 ? 2.0 :
        score >= 50 ? 1.7 :
        score >= 45 ? 1.3 :
        score >= 40 ? 1.0 : 0.7;

    private static double? WeightedAverage(IEnumerable<(double? Average, int Count)> values)
    {
        var rows = values.Where(value => value.Average != null && value.Count > 0).ToList();
        if (rows.Count == 0) return null;
        var count = rows.Sum(value => value.Count);
        return Math.Round(rows.Sum(value => value.Average!.Value * value.Count) / count, 1,
            MidpointRounding.AwayFromZero);
    }
}
