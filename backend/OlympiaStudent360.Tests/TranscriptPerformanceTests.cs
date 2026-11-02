using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    private static async Task<int> SeedTranscriptPerformanceData(AppDbContext db)
    {
        var first = new StudentProfile { SISUserId = "performance-a", FullName = "Performance student A", GradeLevel = "12", Homeroom = "11H1" };
        var second = new StudentProfile { SISUserId = "performance-b", FullName = "Performance student B", GradeLevel = "12", Homeroom = "11H2" };
        string[] subjects = ["Math", "English", "Physics", "Chemistry", "Biology", "History"];
        for (var studentIndex = 0; studentIndex < 2; studentIndex++)
        for (var subjectIndex = 0; subjectIndex < subjects.Length; subjectIndex++)
        {
            var id = 1500 + studentIndex * 100 + subjectIndex;
            var course = new CanvasCourse { CanvasCourseId = id, Name = $"{subjects[subjectIndex]} 11H{studentIndex + 1}", EnrollmentTermName = "2025-2026", CanvasGradeLevel = "12" };
            var enrollment = new CanvasEnrollment { CanvasEnrollmentId = id, CanvasUserId = studentIndex + 1, Course = course, Student = studentIndex == 0 ? first : second };
            for (var semester = 1; semester <= 2; semester++)
                db.CanvasCourseScores.Add(new() { WarehouseScoreFactId = id * 10 + semester, Enrollment = enrollment,
                    Student = enrollment.Student, GradingPeriodId = semester, GradingPeriodTitle = $"HK{semester}",
                    EnrollmentOverrideScore = semester == 1 ? 80 : 90 });
        }
        await db.SaveChangesAsync();
        return first.Id;
    }

    private sealed class TranscriptSqlCapture : DbCommandInterceptor
    {
        public readonly ConcurrentQueue<string> Commands = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
        public int ScoreReads => Commands.Count(sql => sql.Contains("CanvasCourseScores"));
    }

    [MySqlFact]
    public async Task TranscriptCatalogDoesNotReadMarksAndUnfilteredSchoolRequestsAreRejected()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using (var seed = test.Open()) await SeedTranscriptPerformanceData(seed);
        var capture = new TranscriptSqlCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(capture).Options);
        var controller = TranscriptController(db);
        var catalog = TranscriptValue(await controller.GetCatalog());
        Assert.Equal("2025-2026", Assert.Single(catalog.Years).AcademicYear);
        Assert.Equal(6, catalog.SubjectOptions.Count);
        Assert.Equal(0, capture.ScoreReads);
        Assert.DoesNotContain(capture.Commands, sql => sql.Contains("CanvasSubmissions"));
        var reads = capture.Commands.Count;
        Assert.IsType<BadRequestObjectResult>((await controller.GetOverallTranscript()).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.GetOverallTranscript(academicYear: "2025-2026")).Result);
        Assert.Equal(reads, capture.Commands.Count);
    }

    [MySqlFact]
    public async Task TranscriptCacheReusesSchoolSnapshotAcrossPagesAndFiltersWithoutCrossingAccessScopes()
    {
        await using var test = await TestDatabase.CreateAsync();
        int studentId;
        await using (var seed = test.Open()) studentId = await SeedTranscriptPerformanceData(seed);
        var capture = new TranscriptSqlCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(capture).Options);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var requests = new InFlightRequestCoalescer();
        var controller = TranscriptController(db, cache: cache, requests: requests);
        var first = TranscriptValue(await controller.GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11", subjectPage: 1, subjectPageSize: 5));
        Assert.Equal(2, first.TotalStudents);
        Assert.Equal(6, first.TotalSubjects);
        Assert.All(first.Years[0].Grades, grade => Assert.Equal(5, grade.Subjects.Count));
        Assert.All(capture.Commands.Where(sql => sql.Contains("CanvasCourseScores")), sql =>
        {
            Assert.Contains("UNION", sql);
            Assert.DoesNotContain("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        });
        var reads = capture.ScoreReads;
        var commandCount = capture.Commands.Count;
        var second = TranscriptValue(await controller.GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11", subjectPage: 2, subjectPageSize: 5, excludedSubjects: ["math"]));
        Assert.Equal(reads, capture.ScoreReads);
        Assert.Equal(commandCount + 1, capture.Commands.Count); // Only the lightweight source-version read.
        Assert.Equal(5, second.TotalSubjects);
        Assert.All(second.Years[0].Grades, grade => Assert.Empty(grade.Subjects));
        var restored = TranscriptValue(await controller.GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11"));
        Assert.All(restored.Years[0].Grades, grade => Assert.Equal(6, grade.Subjects.Count));
        Assert.Equal(first.OverallGpa, restored.OverallGpa);

        var restricted = new UserAccessSnapshot("teacher", "Teacher", new HashSet<string> { TeacherAccessScopes.Class }, ["11H1"], []);
        var scoped = TranscriptValue(await TranscriptController(db, restricted, cache, requests).GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11"));
        Assert.Equal(1, scoped.TotalStudents);
        Assert.Equal("11", Assert.Single(Assert.Single(scoped.Years).Grades).GradeLevel);

        // An edit made by another request/instance must invalidate the already-cached summary.
        await using (var write = test.Open())
            TranscriptValue(await TranscriptController(write).SaveRecord(studentId, new() {
                Years = [new() { AcademicYear = "2025-2026", GradeLevel = "10" }] }));
        var updated = TranscriptValue(await controller.GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11"));
        Assert.Equal(1, updated.TotalStudents);
        var moved = TranscriptValue(await controller.GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "10"));
        Assert.Equal(1, moved.TotalStudents);
        Assert.Equal("10", Assert.Single(Assert.Single(moved.Years).Grades).GradeLevel);
        Assert.True(capture.ScoreReads > reads);
    }

    [MySqlFact]
    public async Task TranscriptColdRequestsShareOneComputationAndAbortedRequestsCancel()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using (var seed = test.Open()) await SeedTranscriptPerformanceData(seed);
        var capture = new TranscriptSqlCapture();
        var options = new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(capture).Options;
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var requests = new InFlightRequestCoalescer();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = new AppDbContext(options);
            Assert.Equal(2, TranscriptValue(await TranscriptController(db, cache: cache, requests: requests).GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11")).TotalStudents);
        }));
        Assert.Equal(2, capture.ScoreReads); // One grouped score read and one student count for the entire burst.
        await using var canceledDb = new AppDbContext(options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TranscriptController(canceledDb, cache: cache, requests: requests)
            .GetOverallTranscript(academicYear: "2025-2026", gradeLevel: "11", cancellationToken: cancellation.Token));
    }
}
