using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    private static T SubjectResult<T>(ActionResult<T> result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [MySqlFact]
    public async Task SubjectSupportUsesConfiguredThresholdAndSubjectRosterForSummaryAndDetails()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var students = Enumerable.Range(1, 6).Select(index => new StudentProfile
        {
            SISUserId = "subject-" + index, SISStudentCode = "S0" + index, FullName = "Student " + index,
            GradeLevel = index == 5 ? "9" : "8", Homeroom = index == 5 ? "9A" : "8A",
            SISIsActive = index != 6
        }).ToArray();
        db.StudentProfiles.AddRange(students);
        db.StudentClassificationSettings.Add(new StudentClassificationSetting { HighRiskCanvasAverageBelow = 65 });
        db.CanvasAccounts.AddRange(new CanvasAccount { CanvasAccountId = 20, Name = "G8_Math" },
            new CanvasAccount { CanvasAccountId = 21, Name = "G9_Math" },
            new CanvasAccount { CanvasAccountId = 30, Name = "G8_Science" },
            new CanvasAccount { CanvasAccountId = 40, Name = "G9_History" });
        var math = new CanvasCourse { CanvasCourseId = 20, CanvasAccountId = 20, Name = "Math 8", CanvasGradeLevel = "8", EnrollmentTermName = "2026-27" };
        var science = new CanvasCourse { CanvasCourseId = 30, CanvasAccountId = 30, Name = "Science 8", CanvasGradeLevel = "8", EnrollmentTermName = "2026-2027" };
        var mathNine = new CanvasCourse { CanvasCourseId = 21, CanvasAccountId = 21, Name = "Math 9", CanvasGradeLevel = "9", EnrollmentTermName = "2026-2027" };
        var history = new CanvasCourse { CanvasCourseId = 40, CanvasAccountId = 40, Name = "History 9", CanvasGradeLevel = "9", EnrollmentTermName = "2026-2027" };
        var oldMath = new CanvasCourse { CanvasCourseId = 19, CanvasAccountId = 20, Name = "Old Math", CanvasGradeLevel = "8", EnrollmentTermName = "2025-2026" };
        db.CanvasCourses.AddRange(math, science, mathNine, history, oldMath);
        long factId = 1000;
        void Enroll(StudentProfile student, CanvasCourse course, double? mark, string? grade = null, bool old = false)
        {
            var enrollment = new CanvasEnrollment { CanvasEnrollmentId = ++factId, Student = student, Course = course,
                CanvasUserId = factId, Type = "StudentEnrollment", EnrollmentState = "active" };
            db.CanvasEnrollments.Add(enrollment);
            db.CanvasCourseScores.Add(new CanvasCourseScore { WarehouseScoreFactId = ++factId, Enrollment = enrollment,
                Student = student, FinalScore = mark, FinalGrade = grade, GradingPeriodId = 1, GradingPeriodTitle = "HK1",
                GradingPeriodStartDate = new(old ? 2025 : 2026, 9, 1), GradingPeriodEndDate = new(old ? 2026 : 2027, 1, 20) });
        }
        Enroll(students[0], math, 60); Enroll(students[1], math, 65);
        Enroll(students[2], math, 0); // Canvas placeholder, no grading evidence.
        Enroll(students[3], math, 0, "F"); // Genuine graded zero.
        foreach (var student in students.Take(4)) Enroll(student, science, student == students[0] ? 95 : student == students[1] ? 30 : null);
        Enroll(students[4], mathNine, 10); Enroll(students[4], history, 10);
        Enroll(students[5], math, 1); // SIS says inactive.
        Enroll(students[0], oldMath, 1, old: true);
        db.StudentBehaviours.Add(new StudentBehaviour { Student = students[1], BehaviourType = "Đi học muộn", IncidentDate = DateTime.UtcNow });
        var firstEnrollment = db.CanvasEnrollments.Local.First(row => row.Student == students[0] && row.Course == math);
        db.CanvasCourseScores.Add(new CanvasCourseScore { WarehouseScoreFactId = ++factId, Enrollment = firstEnrollment,
            Student = students[0], FinalScore = 0, GradingPeriodId = 2, GradingPeriodTitle = "HK2",
            GradingPeriodStartDate = new(2027, 1, 21), GradingPeriodEndDate = new(2027, 6, 30) });
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Items[UserAccessService.HttpContextItemKey] = GradeAccess("8");
        var controller = new SubjectSupportReportsController(db,
            new UserAccessService(db, new HttpContextAccessor { HttpContext = http }), new CanvasReportingPeriodService(db));
        var summary = SubjectResult(await controller.Summary("2026-2027", "hk1", 20));
        Assert.Equal(65, summary.LowScoreThreshold);
        Assert.DoesNotContain(summary.Subjects, subject => subject.Name == "History");
        var cohort = Assert.Single(summary.ClassDistribution);
        Assert.Equal("8A", cohort.Homeroom);
        Assert.Equal(4, cohort.Students);
        Assert.Equal(2, cohort.AtRisk);
        Assert.Equal(1, cohort.NoScoreStudents);
        Assert.Equal(3, cohort.ScoredStudents);
        Assert.Equal(41.7m, cohort.AverageCanvasScorePercentage);
        var details = SubjectResult(await controller.Students(20, "2026-2027", "hk1", "8", "8A", true));
        Assert.Equal(cohort.AtRisk, details.TotalCount);
        Assert.Equal(new[] { students[0].Id, students[3].Id }, details.Items.Select(student => student.Id).ToArray());
        var allStudents = SubjectResult(await controller.Students(20, "2026-2027", "hk1", "8", "8A"));
        Assert.Equal(cohort.Students, allStudents.TotalCount);
        Assert.Null(allStudents.Items.Single(student => student.Id == students[2].Id).AverageCanvasScorePercentage);
        Assert.False(allStudents.Items.Single(student => student.Id == students[1].Id).NeedsSupport); // Equal to threshold; behaviour irrelevant.
        Assert.Equal(1, SubjectResult(await controller.Students(20, "2026-2027", "hk1", "8", "8A", true, "S04")).TotalCount);
        var scienceSummary = SubjectResult(await controller.Summary("2026-2027", "hk1", 30));
        Assert.Equal(1, Assert.Single(scienceSummary.ClassDistribution).AtRisk);
        Assert.Equal(students[1].Id, Assert.Single(SubjectResult(await controller.Students(30, "2026-2027", "hk1", supportOnly: true)).Items).Id);
        Assert.IsType<NotFoundResult>((await controller.Summary("2026-2027", "hk1", 40)).Result);
        var missing = SubjectResult(await controller.Summary("2026-2027", "hk2", 20));
        Assert.Equal(0, Assert.Single(missing.ClassDistribution).AtRisk);
        Assert.Equal(4, Assert.Single(missing.ClassDistribution).NoScoreStudents);
        var settings = await db.StudentClassificationSettings.SingleAsync();
        settings.HighRiskCanvasAverageBelow = 50;
        await db.SaveChangesAsync();
        Assert.Equal(1, Assert.Single(SubjectResult(await controller.Summary("2026-2027", "hk1", 20)).ClassDistribution).AtRisk);
        http.Items[UserAccessService.HttpContextItemKey] = new UserAccessSnapshot("health", "Teacher", new HashSet<string> { "Health" }, [], []);
        Assert.IsType<ForbidResult>((await controller.Summary("2026-2027", "hk1", 20)).Result);
        Assert.IsType<ForbidResult>((await controller.Students(20)).Result);
    }

    [MySqlFact]
    public async Task IndividualHs1SelectionsFilterExistingFactsAcrossAllAcademicReports()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "assessments", FullName = "Assessment student", GradeLevel = "8", Homeroom = "8A" };
        var course = new CanvasCourse { CanvasCourseId = 100, Name = "Math 8", CanvasGradeLevel = "8", CanvasAccountId = 20 };
        db.CanvasAccounts.Add(new CanvasAccount { CanvasAccountId = 20, Name = "G8_Math" });
        db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild { SchemaVersion = 7, CompletedAtUtc = DateTime.UtcNow });
        foreach (var index in Enumerable.Range(1, 4))
        {
            var name = "HK1-HS1." + index;
            var assignment = new CanvasAssignment { CanvasAssignmentId = 1000 + index, Course = course, AssignmentGroupName = name, Name = name };
            db.CanvasDashboardMetricAggregates.Add(new CanvasDashboardMetricAggregate
            {
                Student = student, Course = course, Assignment = assignment, AcademicYear = "2026-2027", Term = "hk1",
                MetricKey = CanvasDashboardAggregateService.SubmissionScoreAverage, ValueSum = index * 20, SampleCount = 1
            });
            db.CanvasLeadershipReportAggregates.Add(new CanvasLeadershipReportAggregate
            {
                Student = student, Course = course, AcademicYear = "2026-2027", Term = "hk1",
                AssignmentGroupName = name, AssessmentCategory = "HS1", EarnedPoints = index * 20, PossiblePoints = 100,
                ScoreSum = index * 20, ScoreCount = 1, Graded = 1, Submitted = 1
            });
        }
        await db.SaveChangesAsync();
        var originalIds = await db.CanvasLeadershipReportAggregates.Select(row => row.Id).ToArrayAsync();
        var access = new UserAccessService(db, new HttpContextAccessor());
        var scores = new ScoreReportsController(db, access);
        var leadership = new LeadershipReportsController(db, access);
        var learning = new LearningReportsController(db, access);
        Assert.Equal(new[] { "all", "HS1", "HS1.1", "HS1.2", "HS1.3", "HS1.4" }, SubjectResult(await scores.Options(default)).AssessmentCategories);
        Assert.Contains("HS1.3", SubjectResult(await leadership.Options(default)).AssessmentCategories);
        Assert.Contains("HS1.4", SubjectResult(await learning.Options(default)).AssessmentCategories);
        foreach (var index in Enumerable.Range(1, 4))
        {
            var category = "HS1." + index;
            var score = SubjectResult(await scores.Run(new ScoreReportQuery
            { ViewKey = "assessment-average", AcademicYear = "2026-2027", Term = "hk1", AssessmentCategory = category }, default));
            Assert.Equal(index * 2, score.Total.AverageScore);
            var result = SubjectResult(await leadership.Run(new LeadershipReportQuery
            { ReportKey = "assessment-results", AcademicYear = "2026-2027", Term = "hk1", AssessmentCategory = category, Export = true }, default));
            Assert.Equal(index * 20, result.Summary.AverageScorePercent);
            Assert.Equal("HK1-" + category, Assert.Single(result.Rows).AssignmentGroupName);
            Assert.Equal(category, result.Rows[0].AssessmentCategory);
            var review = SubjectResult(await learning.Run(new LearningReportQuery
            { ReportKey = "homework-achievement", AcademicYear = "2026-2027", Term = "hk1", AssessmentCategory = category, MinScore = 0 }, default));
            Assert.Equal(index * 20, Assert.Single(review.Rows).Score);
        }
        var combined = SubjectResult(await scores.Run(new ScoreReportQuery
        { ViewKey = "assessment-average", AcademicYear = "2026-2027", Term = "hk1", AssessmentCategory = "HS1" }, default));
        Assert.Equal(5, combined.Total.AverageScore);
        Assert.Equal(originalIds, await db.CanvasLeadershipReportAggregates.Select(row => row.Id).ToArrayAsync());
        Assert.IsType<BadRequestObjectResult>((await scores.Run(new ScoreReportQuery
        { ViewKey = "assessment-average", AcademicYear = "2026-2027", Term = "hk1", AssessmentCategory = "HS1.10" }, default)).Result);
    }
}
