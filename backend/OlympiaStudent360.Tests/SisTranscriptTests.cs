using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.SisSync;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class SisGradebookContractTests
{
    [Fact]
    public void PreservesZeroAndCompetencyMarksAndRejectsOutOfRangeOrConflictingGrades()
    {
        using var zero = JsonDocument.Parse(JsonSerializer.Serialize(SisGradebookFixtures.Cell("10", 4, score: 0)));
        using var competency = JsonDocument.Parse(JsonSerializer.Serialize(SisGradebookFixtures.Cell("11", 1, letter: "HTT")));
        using var secondary = JsonDocument.Parse(JsonSerializer.Serialize(SisGradebookFixtures.Cell("12", 5, score: 9)));
        using var ambiguous = JsonDocument.Parse("""{"grade":{"code":"04","name":"Khối 5"}}""");
        Assert.Equal("4", SisGradebookCell.PrimaryGrade(zero.RootElement));
        Assert.Equal("1", SisGradebookCell.PrimaryGrade(competency.RootElement));
        Assert.Null(SisGradebookCell.PrimaryGrade(secondary.RootElement));
        Assert.Null(SisGradebookCell.PrimaryGrade(ambiguous.RootElement));

        var marked = SisGradebookCell.Read(zero.RootElement, "4");
        Assert.Equal(0m, marked.Score);
        Assert.Equal(10m, marked.MaxScore);
        Assert.Equal("SEMESTER_2", marked.Semester);
        Assert.Equal("FINAL", marked.Stage);
        Assert.Equal("Source comment", marked.Comment);
        var rated = SisGradebookCell.Read(competency.RootElement, "1");
        Assert.Null(rated.Score);
        Assert.Equal("HTT", rated.Letter);
    }
}

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task SisGradebooksBackfillAndReplayOnlyPrimaryMarksForAccessibleStudents()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        using (var roster = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, roster, SisFixtures.Options(), true, default);

        var options = SisFixtures.Options(schoolYearId: "277", grades: true);
        var queries = new List<string>();
        async Task Sync(params object[] marks)
        {
            using var http = SisFixtures.Http([SisFixtures.Student(year: "277")], gradebooks: marks, gradeQueries: queries);
            await new SisSyncRunner().RunAsync(db, http, options, false, default);
            db.ChangeTracker.Clear();
        }
        await Sync(SisGradebookFixtures.Cell("20", 4, score: 0), SisGradebookFixtures.Cell("21", 4, letter: "HTT"),
            SisGradebookFixtures.Cell("22", 5, score: 9));

        var profile = await db.StudentProfiles.SingleAsync();
        Assert.Equal("6", profile.GradeLevel);
        var httpContext = new DefaultHttpContext();
        var access = new UserAccessService(db, new HttpContextAccessor { HttpContext = httpContext });
        httpContext.Items[UserAccessService.HttpContextItemKey] =
            new UserAccessSnapshot(null, "Admin", new HashSet<string>(), [], []);
        var transcript = new SisTranscriptsController(db, access);
        async Task<SisTranscriptDto> Read() => Assert.IsType<SisTranscriptDto>(
            Assert.IsType<OkObjectResult>((await transcript.Student(profile.Id)).Result).Value);
        var directory = Assert.IsType<SisTranscriptDirectoryDto>(
            Assert.IsType<OkObjectResult>((await transcript.Students("OS001")).Result).Value);
        Assert.Equal(profile.Id, Assert.Single(directory.Students).Id);
        var first = await Read();
        Assert.Equal(2, first.Marks.Count);
        Assert.All(first.Marks, mark => { Assert.Equal("4", mark.GradeLevel); Assert.Equal("277", mark.SchoolYearId); });
        Assert.Contains(first.Marks, mark => mark.Score == 0);
        Assert.Contains(first.Marks, mark => mark.Letter == "HTT");
        var catalog = Assert.IsType<SisTranscriptYearDto[]>(
            Assert.IsType<OkObjectResult>((await transcript.SummaryCatalog()).Result).Value);
        var year = Assert.Single(catalog);
        Assert.Equal("277", year.Id);
        Assert.Equal("2025–2026", year.Name);
        Assert.Equal("4", Assert.Single(year.Grades));
        var summary = Assert.IsType<SisTranscriptSummaryDto>(
            Assert.IsType<OkObjectResult>((await transcript.Summary(year.Id, "4")).Result).Value);
        Assert.Equal(1, summary.Students);
        var items = Assert.Single(summary.Subjects).Items;
        Assert.Contains(items, item => item.Average == 0m && item.MaxScore == 10m);
        Assert.Equal(new SisTranscriptRatingDto("HTT", 1),
            Assert.Single(Assert.Single(items, item => item.Average is null).Ratings));
        Assert.IsType<BadRequestObjectResult>((await transcript.Summary(year.Id, "5")).Result);
        Assert.Equal(2, await db.SisWarehouseRecords.CountAsync(row => row.Endpoint == SisEndpoints.Gradebooks));
        Assert.Empty(await db.CanvasCourseScores.ToListAsync());

        await Sync(SisGradebookFixtures.Cell("20", 4, deleted: true), SisGradebookFixtures.Cell("21", 4, letter: "HT"));
        Assert.Equal("HT", Assert.Single((await Read()).Marks).Letter);
        Assert.All(queries, query => Assert.Contains("school_year_id=277", query));
        Assert.DoesNotContain("updated_since", queries[0]);
        Assert.Contains("updated_since", queries[1]);

        await Sync(SisGradebookFixtures.Cell("20", 4, score: 8), SisGradebookFixtures.Cell("21", 5, letter: "HT"));
        Assert.Equal(8m, Assert.Single((await Read()).Marks).Score);
        httpContext.Items[UserAccessService.HttpContextItemKey] =
            new UserAccessSnapshot(null, "Teacher", new HashSet<string> { TeacherAccessScopes.Class }, ["4A"], []);
        Assert.IsType<NotFoundResult>((await transcript.Student(profile.Id)).Result);
        Assert.Empty(Assert.IsType<SisTranscriptDirectoryDto>(
            Assert.IsType<OkObjectResult>((await transcript.Students(null)).Result).Value).Students);
        Assert.Empty(Assert.IsType<SisTranscriptYearDto[]>(
            Assert.IsType<OkObjectResult>((await transcript.SummaryCatalog()).Result).Value));
        Assert.Empty(Assert.IsType<SisTranscriptSummaryDto>(
            Assert.IsType<OkObjectResult>((await transcript.Summary(year.Id, "4")).Result).Value).Subjects);
        httpContext.Items[UserAccessService.HttpContextItemKey] =
            new UserAccessSnapshot(null, "Teacher", new HashSet<string> { TeacherAccessScopes.Health }, [], []);
        Assert.IsType<ForbidResult>((await transcript.Student(profile.Id)).Result);
    }
}

internal static class SisGradebookFixtures
{
    internal static object Cell(string id, int grade, decimal? score = null, string? letter = null, bool deleted = false) => new
    {
        id, student = new { user_id = "120345" }, school = new { id = "10" },
        school_year = new { id = "277", name = "2025–2026" },
        grade = new { id = "304", code = grade.ToString("00"), name = $"Khối {grade}" },
        subject = new { id = "501", name = "Toán" },
        grade_item = new { id, name = "Cuối kỳ", max_score = letter is null ? (int?)10 : null },
        semester = "SEMESTER_2", stage = "FINAL", period_name = "Cuối HK 2",
        homeroom_class = new { name = "4A" }, score, letter, comment = "Source comment",
        is_deleted = deleted, updated_at = "2026-10-31T08:00:00+07:00"
    };
}
