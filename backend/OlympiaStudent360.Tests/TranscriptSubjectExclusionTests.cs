using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task TranscriptExclusionsApplyBeforePagingAndKeepCatalogAndRecordedAverages()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile
        {
            SISUserId = "transcript-settings", FullName = "Transcript settings student",
            GradeLevel = "10", Homeroom = "10H1"
        };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        string[] codes = ["math", "physics", "chemistry", "biology", "literature", "history", "geography",
            "english", "ict", "music", "fine-arts", "physical-education", "personal-development"];
        var options = new TranscriptSubjectOptions();
        for (var index = 0; index < codes.Length; index++)
        {
            var courseId = 100 + index;
            options.CanvasCourseIds[courseId] = codes[index];
            var course = new CanvasCourse
            {
                CanvasCourseId = courseId, Name = $"{codes[index]} 10H1", CanvasGradeLevel = "10",
                EnrollmentTermName = "2025-2026"
            };
            var enrollment = new CanvasEnrollment
            {
                CanvasEnrollmentId = courseId, Course = course, CanvasUserId = 10,
                Student = student, Type = "StudentEnrollment", EnrollmentState = "active"
            };
            for (var semester = 1; semester <= 2; semester++)
                db.CanvasCourseScores.Add(new CanvasCourseScore
                {
                    WarehouseScoreFactId = courseId * 10 + semester, Enrollment = enrollment,
                    Student = student, GradingPeriodId = semester, GradingPeriodTitle = $"HK{semester}",
                    GradingPeriodEndDate = new DateTime(2026, semester == 1 ? 1 : 6, 15),
                    CurrentScore = 50 + index * 3 + semester
                });
        }
        await db.SaveChangesAsync();
        var controller = new TranscriptsController(db, new UserAccessService(db, new HttpContextAccessor()),
            new TranscriptSubjectResolver(Options.Create(options)));
        static T Value<T>(ActionResult<T> response) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(response.Result).Value);
        static SchoolTranscriptGradeDto Grade(SchoolTranscriptDto record) => Assert.Single(Assert.Single(record.Years).Grades);

        var original = Value(await controller.GetOverallTranscript(subjectPage: 1));
        Assert.Equal(13, original.TotalSubjects);
        Assert.Equal(10, Grade(original).Subjects.Count);
        Assert.Equal(2, Grade(original).TotalSubjectPages);
        var completeStudent = Value(await controller.GetOverallTranscript(studentId: student.Id));
        Assert.Equal(13, Grade(completeStudent).Subjects.Count);

        string[] excluded = [" MATH ", "MUSIC", "physical-education"];
        var filtered = Value(await controller.GetOverallTranscript(subjectPage: 1, excludedSubjects: excluded));
        Assert.Equal(10, filtered.TotalSubjects);
        Assert.Equal(10, Grade(filtered).Subjects.Count);
        Assert.Equal(1, Grade(filtered).TotalSubjectPages);
        Assert.Equal(13, filtered.SubjectOptions.Count);
        Assert.Contains(filtered.SubjectOptions, subject => subject.SubjectCode == "music");
        Assert.DoesNotContain(Grade(filtered).Subjects, subject => subject.SubjectCode is "math" or "music" or "physical-education");
        Assert.Equal(original.OverallAverage, filtered.OverallAverage);
        Assert.Equal(original.OverallGpa, filtered.OverallGpa);
        Assert.Equal(Grade(original).Gpa, Grade(filtered).Gpa);

        var filteredStudent = Value(await controller.GetOverallTranscript(studentId: student.Id, excludedSubjects: excluded));
        Assert.Equal(10, Grade(filteredStudent).Subjects.Count);
        Assert.Equal(completeStudent.OverallGpa, filteredStudent.OverallGpa);

        var details = Value(await controller.GetGradeDetails("2025-2026", "10", excludedSubjects: excluded));
        Assert.Equal(10, details.TotalSubjects);
        Assert.Equal(20, details.MatchingRecords);
        Assert.DoesNotContain(details.Records, record => record.CanvasCourseId is 100 or 109 or 111);
        Assert.Equal(Grade(original).Gpa, details.Gpa);

        var allHidden = Value(await controller.GetOverallTranscript(subjectPage: 1, excludedSubjects: codes));
        Assert.Empty(Grade(allHidden).Subjects);
        Assert.Equal(0, Grade(allHidden).TotalSubjectPages);
        Assert.Equal(13, allHidden.SubjectOptions.Count);
        Assert.Equal(original.OverallGpa, allHidden.OverallGpa);
        var restored = Value(await controller.GetOverallTranscript(subjectPage: 1, excludedSubjects: []));
        Assert.Equal(13, restored.TotalSubjects);
        Assert.Equal(2, Grade(restored).TotalSubjectPages);
    }
}
