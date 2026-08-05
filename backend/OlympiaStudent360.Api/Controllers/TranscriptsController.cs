using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/transcripts")]
[ApiController]
[Authorize]
public class TranscriptsController(AppDbContext context) : ControllerBase
{
    private sealed record AggregateRow(
        string AcademicYear,
        string GradeLevel,
        string Subject,
        string Term,
        double Average,
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
                score.CourseScore && score.StudentProfileId == student.Id &&
                score.AcademicYear != null && score.AcademicYear != "" &&
                (score.OverrideScore != null || score.FinalScore != null || score.CurrentScore != null)))
            .Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.StudentCode, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"))
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .Take(limit)
            .Select(student => new TranscriptStudentDto
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel,
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
                score.CourseScore && score.StudentProfileId == student.Id &&
                score.AcademicYear != null && score.AcademicYear != "" &&
                (score.OverrideScore != null || score.FinalScore != null || score.CurrentScore != null)));

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.StudentCode, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(student => new TranscriptStudentDto
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel,
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
    public async Task<ActionResult<SchoolTranscriptDto>> GetOverallTranscript([FromQuery] int? studentId = null)
    {
        var visibleStudentIds = VisibleStudents().Select(student => student.Id);
        StudentProfile? selectedStudent = null;
        if (studentId != null)
        {
            selectedStudent = await VisibleStudents()
                .Include(student => student.SISHealthVisits)
                .FirstOrDefaultAsync(student => student.Id == studentId.Value);
            if (selectedStudent == null)
                return NotFound(new { Message = "Student was not found." });
        }

        var scores = context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.CourseScore && score.StudentProfileId != null &&
                visibleStudentIds.Contains(score.StudentProfileId.Value) &&
                score.AcademicYear != null && score.AcademicYear != "" &&
                score.Student != null && score.Student.GradeLevel != null && score.Student.GradeLevel != "" &&
                (score.OverrideScore != null || score.FinalScore != null || score.CurrentScore != null));
        if (studentId != null)
            scores = scores.Where(score => score.StudentProfileId == studentId.Value);

        var groupedRows = await scores
            .GroupBy(score => new
            {
                AcademicYear = score.AcademicYear!,
                // SIS membership is the historical source of truth. The profile
                // grade is only a fallback while older SIS memberships are absent.
                GradeLevel = context.SISClassStudents
                    .Where(membership => membership.StudentProfileId == score.StudentProfileId &&
                        membership.SISClass.AcademicYear == score.AcademicYear &&
                        membership.SISClass.GradeLevel != null && membership.SISClass.GradeLevel != "")
                    .Select(membership => membership.SISClass.GradeLevel)
                    .FirstOrDefault() ?? score.Student!.GradeLevel!,
                Subject = score.Subject ?? score.CourseName ?? "—",
                Term = score.Term ?? score.GradingPeriodTitle ?? "Cả năm"
            })
            .Select(group => new
            {
                group.Key.AcademicYear,
                group.Key.GradeLevel,
                group.Key.Subject,
                group.Key.Term,
                Average = group.Average(score => score.OverrideScore ?? score.FinalScore ?? score.CurrentScore)!.Value,
                RecordCount = group.Count()
            })
            .ToListAsync();

        var rows = groupedRows.Select(row => new AggregateRow(
            row.AcademicYear,
            row.GradeLevel,
            row.Subject.Trim(),
            row.Term.Trim(),
            row.Average,
            row.RecordCount)).ToList();
        var derivedRows = rows.Select(row => new
        {
            row.AcademicYear,
            GradeLevel = NormalizeGrade(row.GradeLevel),
            row.Subject,
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
                        var terms = gradeGroup.Select(row => row.Term)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(TermOrder)
                            .ThenBy(term => term, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        var subjects = gradeGroup.GroupBy(row => row.Subject, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(group => SubjectOrder(group.Key))
                            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(subjectGroup =>
                            {
                                var termScores = terms.ToDictionary(
                                    term => term,
                                    term => WeightedAverage(subjectGroup
                                        .Where(row => string.Equals(row.Term, term, StringComparison.OrdinalIgnoreCase))
                                        .Select(row => (row.Average, row.RecordCount))));
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
                                    Subject = subjectGroup.Key,
                                    TermScores = termScores,
                                    Semester1Final = semester1Final,
                                    Semester2Final = semester2Final,
                                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal)
                                        ?? WeightedAverage(subjectGroup.Select(row => (row.Average, row.RecordCount))),
                                    Credit = CreditForSubject(subjectGroup.Key)
                                };
                            }).ToList();
                        return new SchoolTranscriptGradeDto
                        {
                            GradeLevel = gradeGroup.Key,
                            AverageScore = WeightedAverage(gradeGroup.Select(row => (row.Average, row.RecordCount))),
                            Gpa = WeightedGpa(subjects),
                            Terms = terms,
                            Subjects = subjects
                        };
                    }).ToList()
            }).ToList();

        var totalStudents = await scores.Select(score => score.StudentProfileId!.Value).Distinct().CountAsync();
        var allSubjects = years.SelectMany(year => year.Grades).SelectMany(grade => grade.Subjects).ToList();
        return Ok(new SchoolTranscriptDto
        {
            StudentProfileId = selectedStudent?.Id,
            StudentCode = selectedStudent?.StudentCode,
            StudentName = selectedStudent?.FullName,
            DateOfBirth = selectedStudent?.DateOfBirth,
            GradeLevel = selectedStudent?.GradeLevel,
            Homeroom = selectedStudent?.Homeroom,
            Gender = selectedStudent?.Gender ?? selectedStudent?.SISHealthVisits
                .Where(visit => !string.IsNullOrWhiteSpace(visit.Gender))
                .OrderByDescending(visit => visit.ExamTime)
                .Select(visit => visit.Gender)
                .FirstOrDefault(),
            OverallAverage = WeightedAverage(derivedRows.Select(row => (row.Average, row.RecordCount))),
            OverallGpa = WeightedGpa(allSubjects),
            TotalAcademicYears = years.Count,
            TotalStudents = totalStudents,
            TotalSubjects = derivedRows.Select(row => row.Subject).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Years = years
        });
    }

    [HttpGet("{academicYear}/grades/{gradeLevel}")]
    public async Task<ActionResult<SchoolTranscriptGradeDetailDto>> GetGradeDetails(
        string academicYear,
        string gradeLevel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? subject = null,
        [FromQuery] string? term = null)
    {
        academicYear = academicYear.Trim();
        gradeLevel = gradeLevel.Trim();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);

        if (academicYear.Length is < 4 or > 20 || gradeLevel.Length is < 1 or > 50)
            return BadRequest(new { Message = "Invalid academic year or grade level." });

        var visibleStudentIds = VisibleStudents().Select(student => student.Id);
        var historicalGradeStudentIds = context.SISClassStudents.AsNoTracking()
            .Where(membership => membership.SISClass.AcademicYear == academicYear &&
                membership.SISClass.GradeLevel == gradeLevel)
            .Select(membership => membership.StudentProfileId)
            .Distinct();
        var studentsWithHistoricalMembership = context.SISClassStudents.AsNoTracking()
            .Where(membership => membership.SISClass.AcademicYear == academicYear)
            .Select(membership => membership.StudentProfileId)
            .Distinct();

        var baseQuery = context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.CourseScore && score.StudentProfileId != null &&
                visibleStudentIds.Contains(score.StudentProfileId.Value) &&
                score.AcademicYear == academicYear && score.Student != null &&
                (historicalGradeStudentIds.Contains(score.StudentProfileId.Value) ||
                    (!studentsWithHistoricalMembership.Contains(score.StudentProfileId.Value) &&
                        score.Student.GradeLevel == gradeLevel)) &&
                (score.OverrideScore != null || score.FinalScore != null || score.CurrentScore != null));

        var aggregateRows = await baseQuery
            .GroupBy(score => new
            {
                Subject = score.Subject ?? score.CourseName ?? "—",
                Term = score.Term ?? score.GradingPeriodTitle ?? "Cả năm"
            })
            .Select(group => new
            {
                group.Key.Subject,
                group.Key.Term,
                Average = group.Average(score => score.OverrideScore ?? score.FinalScore ?? score.CurrentScore)!.Value,
                RecordCount = group.Count()
            })
            .ToListAsync();

        var terms = aggregateRows.Select(row => row.Term.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(TermOrder)
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var subjects = aggregateRows
            .GroupBy(row => row.Subject.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var semester1Final = WeightedAverage(group.Where(row => IsSemester(row.Term, 1))
                    .Select(row => (row.Average, row.RecordCount)));
                var semester2Final = WeightedAverage(group.Where(row => IsSemester(row.Term, 2))
                    .Select(row => (row.Average, row.RecordCount)));
                var explicitYearFinal = WeightedAverage(group.Where(row => IsYear(row.Term))
                    .Select(row => (row.Average, row.RecordCount)));

                return new TranscriptSubjectDto
                {
                    Subject = group.Key,
                    TermScores = terms.ToDictionary(
                        item => item,
                        item => WeightedAverage(group
                            .Where(row => string.Equals(row.Term.Trim(), item, StringComparison.OrdinalIgnoreCase))
                            .Select(row => (row.Average, row.RecordCount)))),
                    Semester1Final = semester1Final,
                    Semester2Final = semester2Final,
                    YearAverage = AnnualAverage(semester1Final, semester2Final, explicitYearFinal)
                        ?? WeightedAverage(group.Select(row => (row.Average, row.RecordCount))),
                    Credit = CreditForSubject(group.Key)
                };
            }).ToList();

        var recordsQuery = baseQuery;
        if (!string.IsNullOrWhiteSpace(subject))
            recordsQuery = recordsQuery.Where(score => (score.Subject ?? score.CourseName) == subject.Trim());
        if (!string.IsNullOrWhiteSpace(term))
            recordsQuery = recordsQuery.Where(score => (score.Term ?? score.GradingPeriodTitle) == term.Trim());

        var totalRecords = await recordsQuery.CountAsync();
        var records = await recordsQuery
            .OrderBy(score => score.Subject ?? score.CourseName)
            .ThenBy(score => score.Student!.FullName)
            .ThenBy(score => score.Term ?? score.GradingPeriodTitle)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(score => new TranscriptScoreRecordDto
            {
                CanvasScoreId = score.CanvasScoreId,
                StudentProfileId = score.StudentProfileId!.Value,
                StudentCode = score.Student!.StudentCode,
                StudentName = score.Student.FullName,
                Subject = score.Subject ?? score.CourseName ?? "—",
                CourseName = score.CourseName ?? score.Subject ?? "—",
                Term = score.Term ?? score.GradingPeriodTitle ?? "Cả năm",
                Score = (score.OverrideScore ?? score.FinalScore ?? score.CurrentScore)!.Value,
                ScoreSource = score.OverrideScore != null ? "override" : score.FinalScore != null ? "final" : "current",
                RecordedAt = score.UpdatedAt ?? score.CreatedAt
            })
            .ToListAsync();

        var totalStudents = await baseQuery.Select(score => score.StudentProfileId!.Value).Distinct().CountAsync();
        var allRecordCount = aggregateRows.Sum(row => row.RecordCount);
        return Ok(new SchoolTranscriptGradeDetailDto
        {
            AcademicYear = academicYear,
            GradeLevel = gradeLevel,
            AverageScore = WeightedAverage(aggregateRows.Select(row => (row.Average, row.RecordCount))),
            Gpa = WeightedGpa(subjects),
            TotalStudents = totalStudents,
            TotalSubjects = subjects.Count,
            TotalRecords = allRecordCount,
            MatchingRecords = totalRecords,
            Terms = terms,
            Subjects = subjects,
            Records = records,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize)
        });
    }

    private IQueryable<StudentProfile> VisibleStudents()
        => context.StudentProfiles.AsNoTracking();

    private static string? NormalizeGrade(string gradeLevel)
    {
        if (!int.TryParse(gradeLevel, out var grade)) return gradeLevel;
        return grade is >= 1 and <= 12 ? grade.ToString() : null;
    }

    private static int StartYear(string value) =>
        int.TryParse(value.Split('-', '/', StringSplitOptions.TrimEntries)[0], out var year) ? year : 0;

    private static int TermOrder(string value)
    {
        var normalized = value.ToLowerInvariant();
        if (normalized.Contains('1')) return 1;
        if (normalized.Contains('2')) return 2;
        if (normalized.Contains('3')) return 3;
        if (normalized.Contains('4')) return 4;
        return normalized is "cả năm" or "year" or "full year" ? 9 : 8;
    }

    private static bool IsSemester(string value, int semester)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains($"semester {semester}") ||
            normalized.Contains($"semester{semester}") ||
            normalized.Contains($"term {semester}") ||
            normalized.Contains($"term{semester}") ||
            normalized.Contains($"học kỳ {semester}") ||
            normalized.Contains($"hk{semester}") ||
            normalized == $"s{semester}" ||
            normalized == $"s{semester}f";
    }

    private static bool IsYear(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "cả năm" or "year" or "full year" or "yr" or "year final";
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

    private static double CreditForSubject(string subject)
    {
        var normalized = subject.Trim().ToLowerInvariant();
        return normalized.Contains("entrepreneurship") ||
            normalized.Contains("travel and tourism") ||
            normalized.Contains("ielts prep")
            ? 0.5
            : 1;
    }

    private static int SubjectOrder(string subject)
    {
        var normalized = subject.Trim().ToLowerInvariant();
        var order = new[]
        {
            "math", "physics", "chemistry", "biology", "literature", "history", "geography",
            "english", "national defense", "economics", "technology", "ict", "music",
            "volleyball", "piano", "entrepreneurship", "travel and tourism", "psat", "ielts"
        };
        var index = Array.FindIndex(order, item => normalized.Contains(item));
        return index < 0 ? int.MaxValue : index;
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

    private static double? WeightedAverage(IEnumerable<(double Average, int Count)> values)
    {
        var rows = values.Where(value => value.Count > 0).ToList();
        if (rows.Count == 0) return null;
        var count = rows.Sum(value => value.Count);
        return Math.Round(rows.Sum(value => value.Average * value.Count) / count, 1, MidpointRounding.AwayFromZero);
    }
}
