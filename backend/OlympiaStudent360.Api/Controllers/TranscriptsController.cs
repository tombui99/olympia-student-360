using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
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

    [HttpGet("overall")]
    public async Task<ActionResult<SchoolTranscriptDto>> GetOverallTranscript()
    {
        var visibleStudentIds = VisibleStudents().Select(student => student.Id);
        var scores = context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.CourseScore && score.StudentProfileId != null &&
                visibleStudentIds.Contains(score.StudentProfileId.Value) &&
                score.AcademicYear != null && score.AcademicYear != "" &&
                score.Student != null && score.Student.GradeLevel != null && score.Student.GradeLevel != "" &&
                (score.OverrideScore != null || score.FinalScore != null || score.CurrentScore != null));

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
            .OrderByDescending(group => StartYear(group.Key))
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
                            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(subjectGroup =>
                            {
                                var termScores = terms.ToDictionary(
                                    term => term,
                                    term => WeightedAverage(subjectGroup
                                        .Where(row => string.Equals(row.Term, term, StringComparison.OrdinalIgnoreCase))
                                        .Select(row => (row.Average, row.RecordCount))));
                                return new TranscriptSubjectDto
                                {
                                    Subject = subjectGroup.Key,
                                    TermScores = termScores,
                                    YearAverage = WeightedAverage(subjectGroup.Select(row => (row.Average, row.RecordCount)))
                                };
                            }).ToList();
                        return new SchoolTranscriptGradeDto
                        {
                            GradeLevel = gradeGroup.Key,
                            AverageScore = WeightedAverage(gradeGroup.Select(row => (row.Average, row.RecordCount))),
                            Terms = terms,
                            Subjects = subjects
                        };
                    }).ToList()
            }).ToList();

        var totalStudents = await scores.Select(score => score.StudentProfileId!.Value).Distinct().CountAsync();
        return Ok(new SchoolTranscriptDto
        {
            OverallAverage = WeightedAverage(derivedRows.Select(row => (row.Average, row.RecordCount))),
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
            .Select(group => new TranscriptSubjectDto
            {
                Subject = group.Key,
                TermScores = terms.ToDictionary(
                    item => item,
                    item => WeightedAverage(group
                        .Where(row => string.Equals(row.Term.Trim(), item, StringComparison.OrdinalIgnoreCase))
                        .Select(row => (row.Average, row.RecordCount)))),
                YearAverage = WeightedAverage(group.Select(row => (row.Average, row.RecordCount)))
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
    {
        var query = context.StudentProfiles.AsNoTracking();
        if (User.IsInRole("Admin") || User.IsInRole("Principal")) return query;
        return SISClassVisibility.Students(context, User.FindFirstValue(ClaimTypes.NameIdentifier));
    }

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

    private static double? WeightedAverage(IEnumerable<(double Average, int Count)> values)
    {
        var rows = values.Where(value => value.Count > 0).ToList();
        if (rows.Count == 0) return null;
        var count = rows.Sum(value => value.Count);
        return Math.Round(rows.Sum(value => value.Average * value.Count) / count, 1, MidpointRounding.AwayFromZero);
    }
}
