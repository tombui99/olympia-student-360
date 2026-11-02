using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    private static TranscriptsController TranscriptController(AppDbContext db, UserAccessSnapshot? access = null,
        IMemoryCache? cache = null, InFlightRequestCoalescer? requests = null)
    {
        var http = new DefaultHttpContext();
        if (access != null) http.Items[UserAccessService.HttpContextItemKey] = access;
        var options = new TranscriptSubjectOptions();
        options.CanvasCourseIds[501] = "math";
        options.CanvasCourseIds[502] = "english";
        return new TranscriptsController(db, new UserAccessService(db, new HttpContextAccessor { HttpContext = http }),
            new TranscriptSubjectResolver(Options.Create(options)), cache, requests) { ControllerContext = new() { HttpContext = http } };
    }

    private static T TranscriptValue<T>(ActionResult<T> result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [MySqlFact]
    public async Task TranscriptsRequireAssessmentEvidenceAndKeepHistoricalGradesAndFullCredits()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "transcript-review", FullName = "Phan Anh", GradeLevel = "12" };
        var historical = new CanvasCourse { CanvasCourseId = 501, Name = "2526 Math 11E1.1", CanvasGradeLevel = "12", EnrollmentTermName = "2025-2026" };
        var live = new CanvasCourse { CanvasCourseId = 502, Name = "2627 English 12E1.1", CanvasGradeLevel = "12", EnrollmentTermName = "2026-2027" };
        var oldEnrollment = new CanvasEnrollment { CanvasEnrollmentId = 501, CanvasUserId = 1, Course = historical, Student = student };
        var liveEnrollment = new CanvasEnrollment { CanvasEnrollmentId = 502, CanvasUserId = 1, Course = live, Student = student };
        db.CanvasCourseScores.AddRange(
            new() { WarehouseScoreFactId = 5011, Enrollment = oldEnrollment, Student = student, GradingPeriodId = 1,
                GradingPeriodTitle = "S1F", GradesOverrideScore = 80 },
            new() { WarehouseScoreFactId = 5012, Enrollment = oldEnrollment, Student = student, GradingPeriodId = 2,
                GradingPeriodTitle = "S2F", GradesOverrideScore = 90 },
            new() { WarehouseScoreFactId = 5021, Enrollment = liveEnrollment, Student = student, GradingPeriodId = 1,
                GradingPeriodTitle = "S1F", CurrentScore = 100 });
        await db.SaveChangesAsync();
        var controller = TranscriptController(db);
        var record = TranscriptValue(await controller.GetOverallTranscript(student.Id));
        var year = Assert.Single(record.Years);
        Assert.Equal("2025-2026", year.AcademicYear);
        Assert.Equal("11", Assert.Single(year.Grades).GradeLevel);
        Assert.Equal("Anh Phan", record.TranscriptName);
        Assert.Contains("2026-2027", record.AcademicYearOptions);
        var subject = Assert.Single(year.Grades[0].Subjects);
        Assert.Equal(1, subject.Credit);
        Assert.Equal(86.7, subject.YearAverage);
        Assert.False(year.ShowAverage);
        var detail = TranscriptValue(await controller.GetGradeDetails("2025-2026", "11"));
        Assert.Single(detail.Subjects);

        // A real graded zero remains a mark; an empty Canvas aggregate of 100 was suppressed above.
        db.CanvasSubmissions.Add(new() { CanvasSubmissionId = 502, CanvasCourseId = 502, Student = student,
            CanvasUserId = 1, Score = 0, GradedAt = new(2026, 10, 1), WorkflowState = "graded",
            Assignment = new() { CanvasAssignmentId = 502, Course = live, PointsPossible = 100 } });
        liveEnrollment.Scores.Single().CurrentScore = 0;
        await db.SaveChangesAsync();
        record = TranscriptValue(await controller.GetOverallTranscript(student.Id));
        var current = Assert.Single(record.Years.Last().Grades);
        Assert.Equal(0, Assert.Single(current.Subjects).Semester1Final);
        Assert.Equal(1, current.Subjects[0].Credit);
        Assert.Null(current.Subjects[0].YearAverage);
        Assert.Null(current.Gpa);
    }

    [MySqlFact]
    public async Task TranscriptRecordsPersistIbDataAndRejectStaleOrOutOfScopeEdits()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "official-transcript", FullName = "Thái Vũ Minh Châu", GradeLevel = "12", Homeroom = "12E1" };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();
        var controller = TranscriptController(db);
        var model = new TranscriptRecordDto { TranscriptName = "Chau Vu Minh Thai", Notes = "Joined Olympia in Grade 9.",
            EnrollmentDate = new(2023, 9, 1), GraduationDate = new(2027, 6, 1), GraduationExamDate = new(2027, 5, 20), IbCorePoints = 2,
            Years = [new() { AcademicYear = "2026-2027", GradeLevel = "12", ShowAverage = false,
                IbSubjects = [new() { SubjectCode = "math", SubjectName = "Mathematics", Level = "HL", PredictedGrade = "7", Term1 = "6", Term2 = "7" }] }] };
        Assert.Equal(1, TranscriptValue(await controller.SaveRecord(student.Id, model)).Revision);
        await using var readDb = test.Open();
        var read = TranscriptValue(await TranscriptController(readDb).GetOverallTranscript(student.Id));
        Assert.Equal("Joined Olympia in Grade 9.", read.Record.Notes);
        Assert.Equal(new DateOnly(2027, 5, 20), read.Record.GraduationExamDate);
        Assert.Equal("6", read.Record.Years[0].IbSubjects[0].Term1);
        Assert.Equal("12", Assert.Single(Assert.Single(read.Years).Grades).GradeLevel);
        Assert.IsType<ConflictObjectResult>((await controller.SaveRecord(student.Id, new() { Revision = 0 })).Result);

        var restricted = new UserAccessSnapshot("teacher", "Teacher", new HashSet<string> { TeacherAccessScopes.Class }, ["11E1"], []);
        Assert.IsType<NotFoundResult>((await TranscriptController(readDb, restricted).SaveRecord(student.Id, new())).Result);
        var healthOnly = new UserAccessSnapshot("health", "Teacher", new HashSet<string> { TeacherAccessScopes.Health }, [], []);
        Assert.IsType<ForbidResult>((await TranscriptController(readDb, healthOnly).SaveRecord(student.Id, new())).Result);
        Assert.IsType<ForbidResult>((await TranscriptController(readDb, healthOnly).GetOverallTranscript(student.Id)).Result);
    }

    [MySqlFact]
    public async Task TranscriptMigrationAppliesAndMatchesTheCurrentModel()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        Assert.Contains("20261102062329_AddStudentTranscriptRecords", await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.StudentTranscriptRecords.ToListAsync());
    }
}
