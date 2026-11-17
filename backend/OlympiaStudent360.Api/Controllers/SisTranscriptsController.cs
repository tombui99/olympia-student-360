using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.SisSync;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transcripts/sis")]
public sealed class SisTranscriptsController(AppDbContext db, UserAccessService access) : ControllerBase
{
    private IQueryable<StudentProfile> VisibleStudents =>
        access.ApplyStudentScope(db.StudentProfiles.AsNoTracking()).Where(x => x.SchoolOnlineUserId != null);

    private IQueryable<SisWarehouseRecord> PrimaryMarks => db.SisWarehouseRecords.AsNoTracking()
        .Where(row => row.Endpoint == SisEndpoints.Gradebooks && !row.IsDeleted &&
            (row.RecordType == "1" || row.RecordType == "2" || row.RecordType == "3" || row.RecordType == "4"));

    private IQueryable<SisWarehouseRecord> VisibleMarks => PrimaryMarks.Where(mark =>
        VisibleStudents.Any(student => student.SchoolOnlineUserId == mark.StudentUserId));

    [HttpGet("summary/catalog")]
    public async Task<ActionResult<SisTranscriptYearDto[]>> SummaryCatalog(CancellationToken ct = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var marks = VisibleMarks.Where(mark => mark.SchoolYearId != null);
        var groups = marks.GroupBy(mark => new { mark.SchoolYearId, mark.RecordType })
            .Select(group => new { group.Key.SchoolYearId, group.Key.RecordType, SampleId = group.Min(mark => mark.Id) });
        var samples = await (from key in groups
            join sample in db.SisWarehouseRecords.AsNoTracking() on key.SampleId equals sample.Id
            select new { Year = key.SchoolYearId!, Grade = key.RecordType!, sample.PayloadJson }).ToListAsync(ct);
        var years = samples.GroupBy(sample => sample.Year).Select(year =>
        {
            using var document = JsonDocument.Parse(year.First().PayloadJson);
            return new SisTranscriptYearDto(year.Key, SisGradebookCell.SchoolYearName(document.RootElement),
                year.Select(sample => sample.Grade).Order().ToArray());
        }).OrderByDescending(year => year.Name, StringComparer.Ordinal).ThenByDescending(year => year.Id).ToArray();
        return Ok(years);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SisTranscriptSummaryDto>> Summary(
        string? schoolYearId, string? gradeLevel, CancellationToken ct = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        if (schoolYearId is not { Length: > 0 and <= 100 } || !schoolYearId.All(char.IsAsciiDigit) ||
            gradeLevel is not ("1" or "2" or "3" or "4"))
            return BadRequest(new { Message = "Choose a SchoolOnline year and a grade from 1 to 4." });

        var documents = VisibleMarks.Where(mark => mark.SchoolYearId == schoolYearId && mark.RecordType == gradeLevel)
            .Select(mark => new { mark.StudentUserId, mark.PayloadJson }).AsAsyncEnumerable();
        var entries = new List<(string Student, SisTranscriptMarkDto Mark)>();
        await foreach (var row in documents.WithCancellation(ct))
        {
            using var document = JsonDocument.Parse(row.PayloadJson);
            var mark = SisGradebookCell.Read(document.RootElement, gradeLevel);
            if (mark.Score != null || mark.Letter != null)
                entries.Add((row.StudentUserId!, mark));
        }

        var subjects = entries.GroupBy(entry => entry.Mark.SubjectId).Select(subject =>
        {
            var items = subject.GroupBy(entry => new
            {
                entry.Mark.Semester, entry.Mark.Stage, entry.Mark.GradeItemId,
                // Each numeric mean uses a single grading scale. Ratings form their own row.
                Numeric = entry.Mark.Score.HasValue,
                MaxScore = entry.Mark.Score.HasValue ? entry.Mark.MaxScore : null
            }).Select(item =>
            {
                var mark = item.First().Mark;
                var ratings = item.Where(entry => entry.Mark.Letter != null).GroupBy(entry => entry.Mark.Letter!)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new SisTranscriptRatingDto(group.Key, group.Count())).ToArray();
                return new SisTranscriptItemSummaryDto(mark.GradeItem, mark.Semester, mark.Stage,
                    item.Select(entry => entry.Mark.PeriodName).FirstOrDefault(name => name != null),
                    item.Select(entry => entry.Student).Distinct().Count(),
                    item.Average(entry => entry.Mark.Score), item.Key.MaxScore, ratings);
            }).ToArray();
            return new SisTranscriptSubjectSummaryDto(subject.Key, subject.First().Mark.Subject, items);
        }).ToArray();
        return Ok(new SisTranscriptSummaryDto(entries.Select(entry => entry.Student).Distinct().Count(), subjects));
    }

    [HttpGet("students")]
    public async Task<ActionResult<SisTranscriptDirectoryDto>> Students(string? search, int page = 1, CancellationToken ct = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        const int pageSize = 20;
        var marks = PrimaryMarks;
        // Eligibility comes from the historical grade on the marks, not the current roster grade.
        var query = VisibleStudents.Where(student => marks.Any(mark => mark.StudentUserId == student.SchoolOnlineUserId));
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            term = term[..Math.Min(term.Length, 100)];
            var pattern = "%" + term.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
            query = query.Where(x => EF.Functions.Like(x.FullName, pattern, "!") ||
                EF.Functions.Like(x.SISUserId, pattern, "!") ||
                x.SISStudentCode != null && EF.Functions.Like(x.SISStudentCode, pattern, "!") ||
                x.Homeroom != null && EF.Functions.Like(x.Homeroom, pattern, "!"));
        }
        var count = await query.CountAsync(ct);
        var pages = (int)Math.Ceiling(count / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(1, pages));
        var students = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new SisTranscriptStudentDto(x.Id, x.FullName, x.SISStudentCode ?? x.SISUserId, x.Homeroom))
            .ToListAsync(ct);
        return Ok(new SisTranscriptDirectoryDto(students, page, pages));
    }

    [HttpGet("students/{studentId:int}")]
    public async Task<ActionResult<SisTranscriptDto>> Student(int studentId, CancellationToken ct = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var profile = await VisibleStudents.Where(x => x.Id == studentId)
            .Select(x => new { x.SchoolOnlineUserId, Student = new SisTranscriptStudentDto(
                x.Id, x.FullName, x.SISStudentCode ?? x.SISUserId, x.Homeroom) }).SingleOrDefaultAsync(ct);
        if (profile is null) return NotFound();

        var marks = new List<SisTranscriptMarkDto>();
        var documents = PrimaryMarks.Where(x => x.StudentUserId == profile.SchoolOnlineUserId)
            .Select(x => new { x.RecordType, x.PayloadJson }).AsAsyncEnumerable();
        await foreach (var row in documents.WithCancellation(ct))
        {
            using var json = JsonDocument.Parse(row.PayloadJson);
            marks.Add(SisGradebookCell.Read(json.RootElement, row.RecordType!));
        }
        return Ok(new SisTranscriptDto(profile.Student, marks));
    }
}
