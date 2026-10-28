using System.Data.Common;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.CanvasEtl;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OLYMPIA_TEST_MYSQL")))
            Skip = "Set OLYMPIA_TEST_MYSQL to an isolated MySQL server; tests create and drop their own databases.";
    }
}

public sealed class MySqlTheoryAttribute : TheoryAttribute
{
    public MySqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OLYMPIA_TEST_MYSQL")))
            Skip = "Set OLYMPIA_TEST_MYSQL to an isolated MySQL server.";
    }
}

public sealed class MySqlResilienceTests
{
    [MySqlTheory]
    [InlineData("missing-frequency")]
    [InlineData("homework-achievement")]
    [InlineData("reading-completion")]
    [InlineData("grading-coverage")]
    public async Task LearningReportsFilterPersistedActivityDatesAndUpgradeMonthlySnapshots(string reportKey)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "weekly-report", FullName = "Weekly report student", GradeLevel = "5", Homeroom = "5H1" };
        var course = new CanvasCourse { CanvasCourseId = 100, Name = "Test course", EnrollmentTermName = "2026-27", CanvasAccountId = 20, CanvasGradeLevel = "5" };
        db.CanvasAccounts.Add(new CanvasAccount { CanvasAccountId = 20, Name = "G5_Math" });
        db.StudentProfiles.Add(student);
        db.CanvasCourses.Add(course);
        await db.SaveChangesAsync();
        db.CanvasEnrollments.Add(new CanvasEnrollment
        {
            CanvasEnrollmentId = 100, Course = course, CanvasUserId = 10, Student = student,
            Section = new CanvasCourseSection { CanvasSectionId = 100, Course = course, Name = "5H1" },
            Type = "StudentEnrollment", EnrollmentState = "active"
        });
        db.CanvasCourseScores.Add(new CanvasCourseScore
        {
            WarehouseScoreFactId = 100, CanvasEnrollmentId = 100, StudentProfileId = student.Id,
            FinalScore = 75, GradingPeriodTitle = "HK1", GradingPeriodEndDate = new DateTime(2027, 1, 18)
        });
        var dates = new[] { new DateTime(2026, 10, 19), new DateTime(2026, 10, 26),
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 2), new DateTime(2026, 10, 30) };
        for (var index = 0; index < dates.Length; index++)
        {
            var missing = index == 4;
            db.CanvasAssignments.Add(new CanvasAssignment
            {
                CanvasAssignmentId = 5000 + index, CanvasCourseId = 100, Name = $"Task {index}",
                PointsPossible = 100, Published = true, AssignmentGroupName = "HS1",
                DueAt = missing ? dates[0] : dates[index]
            });
            db.CanvasSubmissions.Add(new CanvasSubmission
            {
                CanvasSubmissionId = 6000 + index, CanvasAssignmentId = 5000 + index, CanvasCourseId = 100,
                StudentProfileId = student.Id, CanvasUserId = 10, WorkflowState = missing ? "unsubmitted" : "graded",
                SubmittedAt = missing ? null : dates[index], GradedAt = missing ? null : dates[index],
                Score = missing ? null : 80, Missing = missing, CachedDueDate = missing ? dates[index] : null
            });
        }
        db.CanvasSyncRuns.Add(CompletedRun(new DateTime(2026, 11, 3)));
        await db.SaveChangesAsync();
        await new CanvasDashboardAggregateService(db).RebuildAsync(default);
        var controller = new LearningReportsController(db, new UserAccessService(db, new HttpContextAccessor()));
        async Task<LearningReportResult> Run(int? month, string? week = "2026-10-26", string term = "hk1")
        {
            var response = await controller.Run(new LearningReportQuery
            {
                ReportKey = reportKey, AcademicYear = "2026-2027", Term = term, Month = month, WeekStart = week,
                CourseIds = [100], AssignmentIds = [5000, 5001, 5002, 5003, 5004],
                MaxMissing = 100, MinScore = 0, MinCompletion = 0
            }, default);
            return Assert.IsType<LearningReportResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        }
        var week = await Run(null);
        Assert.True(week.AggregateAvailable);
        var row = Assert.Single(week.Rows);
        if (reportKey == "homework-achievement") Assert.Equal(80, row.Score);
        else Assert.Equal(2, row.Submitted);
        if (reportKey is "missing-frequency" or "reading-completion") Assert.Equal(1, row.Missing);
        if (reportKey == "reading-completion") Assert.Equal(66.7, row.Rate);
        if (reportKey == "grading-coverage") Assert.Equal(2, row.Graded);
        var october = Assert.Single((await Run(10)).Rows);
        var november = Assert.Single((await Run(11)).Rows);
        if (reportKey != "homework-achievement")
        {
            Assert.Equal(1, october.Submitted);
            Assert.Equal(1, november.Submitted);
        }
        if (reportKey == "reading-completion")
        {
            Assert.Equal(50, october.Rate);
            Assert.Equal(100, november.Rate);
        }
        Assert.Empty((await Run(null, term: "hk2")).Rows);

        // A v5 snapshot is still readable for months, but must never masquerade as weekly data.
        await db.CanvasDashboardAggregateBuilds.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SchemaVersion, 5));
        await db.CanvasDashboardMetricAggregates.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AcademicActivityDate, (DateTime?)null));
        Assert.False((await Run(null)).AggregateAvailable);
        Assert.True((await Run(10, week: null)).AggregateAvailable);
        await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default);
        Assert.True((await Run(null)).AggregateAvailable);
        Assert.True(await db.CanvasDashboardMetricAggregates.AnyAsync(item => item.AcademicActivityDate != null));

        if (reportKey == "homework-achievement")
        {
            // Different scores outside the selected week make an accidentally unfiltered query observable.
            await db.CanvasSubmissions.Where(item => item.CanvasAssignmentId == 5000)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Score, 100.0));
            await db.CanvasSubmissions.Where(item => item.CanvasAssignmentId == 5003)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Score, 40.0));
            await new CanvasDashboardAggregateService(db).RebuildAsync(default);
            var access = new UserAccessService(db, new HttpContextAccessor());
            var scores = new ScoreReportsController(db, access);
            foreach (var view in new[] { "subject-grade-average", "subject-letter-distribution", "grade-letter-distribution",
                "class-letter-distribution", "class-subject-distribution", "assessment-average", "score-histogram", "top-students", "bottom-students" })
            {
                async Task<ScoreReportResult> Score(string? start, int? month = null)
                {
                    var response = await scores.Run(new ScoreReportQuery
                    {
                        ViewKey = view, AcademicYear = "2026-2027", Term = "hk1", WeekStart = start, Month = month,
                        Grade = "5", SubaccountIds = [20], CanvasSectionName = "5H1", AssessmentCategory = "HS1"
                    }, default);
                    return Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
                }
                var selected = await Score("2026-10-26", 10);
                Assert.True(selected.AggregateAvailable);
                Assert.True(selected.Total.ScoreCount == 1, $"Expected a scored course result in {view}: {selected.Notice}");
                Assert.Equal(8, selected.Total.AverageScore);
                Assert.Equal(4, (await Score("2026-11-02")).Total.AverageScore);
                Assert.Equal(7.5, (await Score(null)).Total.AverageScore);
                Assert.Equal(8, (await Score("2026-10-26", 11)).Total.AverageScore);
            }
            var leadership = new LeadershipReportsController(db, access);
            foreach (var view in new[] { "learning-engagement", "assessment-results" })
            {
                var response = await leadership.Run(new LeadershipReportQuery
                {
                    ReportKey = view, AcademicYear = "2026-2027", Term = "hk1", WeekStart = "2026-10-26", Month = 10
                }, default);
                var selected = Assert.IsType<LeadershipReportResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
                Assert.True(selected.AggregateAvailable);
                Assert.Equal(1, selected.Summary.Submitted);
                Assert.Equal(1, selected.Summary.Missing);
                Assert.Equal(80, selected.Summary.AverageScorePercent);
                if (view == "assessment-results") Assert.Equal(75, selected.FinalCourseScores!.Summary.AverageScorePercent);
            }
            var weekFacts = await CanvasActivityPeriodFilter.Apply(db.CanvasLeadershipReportAggregates
                .Where(item => !item.IsFinalCourseScore), null, "2026-10-26").ToListAsync();
            Assert.Equal(160, weekFacts.Sum(item => item.EarnedPoints));
            Assert.Equal(200, weekFacts.Sum(item => item.PossiblePoints));
            await db.CanvasDashboardAggregateBuilds.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SchemaVersion, 6));
            var unavailable = await leadership.Run(new LeadershipReportQuery
            { AcademicYear = "2026-2027", WeekStart = "2026-10-26" }, default);
            Assert.False(Assert.IsType<LeadershipReportResult>(Assert.IsType<OkObjectResult>(unavailable.Result).Value).AggregateAvailable);
        }
    }

    [MySqlTheory]
    [InlineData("subject-grade-average")]
    [InlineData("subject-letter-distribution")]
    [InlineData("grade-letter-distribution")]
    [InlineData("class-letter-distribution")]
    [InlineData("class-subject-distribution")]
    [InlineData("assessment-average")]
    [InlineData("current-course-score")]
    [InlineData("score-histogram")]
    [InlineData("top-students")]
    [InlineData("bottom-students")]
    public async Task EnglishScoreReportsFilterEslElaOrBothWithoutChangingStoredFacts(string viewKey)
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        db.CanvasAccounts.AddRange(
            new CanvasAccount { CanvasAccountId = 20, Name = "G5_English/Tiếng Anh" },
            new CanvasAccount { CanvasAccountId = 21, ParentAccountId = 20, Name = "Other courses" },
            new CanvasAccount { CanvasAccountId = 30, Name = "G5_Math/Toán" });
        var student = new StudentProfile { SISUserId = "paired", FullName = "Paired student", GradeLevel = "5", Homeroom = "5H1" };
        (long Id, string Name, string Code, long AccountId, double Score)[] courses =
        [
            (100, "2627 Tiếng Anh/English ESL 5H1", "2627-EnglishESL5H1", 20, 80),
            (101, "2627 Tiếng Anh/English ELA 5H1", "2627-EnglishELA5H1", 20, 100),
            (102, "2627 Tiếng Anh/English OIDP1", "2627-EnglishOIDP1", 20, 40),
            (103, "2627 Nhân văn/Humanities 5H1", "2627-Humanities5H1", 21, 60),
            (200, "2627 Toán/Math 5H1", "2627-Math5H1", 30, 90)
        ];
        foreach (var item in courses)
        {
            var course = new CanvasCourse
            {
                CanvasCourseId = item.Id, Name = item.Name, CourseCode = item.Code,
                CanvasAccountId = item.AccountId, CanvasGradeLevel = "5"
            };
            db.CanvasLeadershipReportAggregates.Add(new CanvasLeadershipReportAggregate
            {
                Student = student, Course = course, AcademicYear = "2026-2027", Term = "hk1",
                AssignmentGroupName = "GHK1", AssessmentCategory = "HS2",
                EarnedPoints = item.Score, PossiblePoints = 100, ScoreSum = item.Score, ScoreCount = 1, Graded = 1
            });
            db.CanvasDashboardMetricAggregates.Add(new CanvasDashboardMetricAggregate
            {
                Student = student, Course = course, AcademicYear = "2026-2027", Term = "hk1",
                MetricKey = CanvasDashboardAggregateService.CurrentScoreAverage,
                ValueSum = item.Score, SampleCount = 1
            });
            db.CanvasEnrollments.Add(new CanvasEnrollment
            {
                CanvasEnrollmentId = item.Id, CanvasUserId = 1, Course = course, Student = student,
                Section = new CanvasCourseSection { CanvasSectionId = item.Id, Course = course, Name = "5H1" },
                Type = "StudentEnrollment", EnrollmentState = "active"
            });
        }
        db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild
        {
            SchemaVersion = 5, LastCompletedSyncRunId = 24, CompletedAtUtc = new DateTime(2026, 10, 20)
        });
        await db.SaveChangesAsync();
        var originalIds = await db.CanvasLeadershipReportAggregates.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).ToListAsync();
        var controller = new ScoreReportsController(db, new UserAccessService(db, new HttpContextAccessor()));
        var request = new ScoreReportQuery
        {
            ViewKey = viewKey, AcademicYear = "2026-2027", Term = "hk1", Grade = "5",
            SubaccountIds = [20], AssessmentCategory = "HS2",
            EnglishProgrammes = ["ESL"],
            CanvasSectionName = viewKey == "class-subject-distribution" ? "5H1" : null
        };
        var response = await controller.Run(request, default);
        var result = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(1, result.Total.ScoreCount);
        Assert.Equal(1, result.Total.StudentCount);
        Assert.Equal(viewKey == "current-course-score" ? 80 : 8, result.Total.AverageScore);
        if (viewKey.Contains("distribution"))
            Assert.Equal((0, 1, 0, 0, 0), (result.Total.APlus, result.Total.A, result.Total.B, result.Total.C, result.Total.D));

        // Every report uses the same selected programme set. Clearing the multi-select
        // means both; one learner's two English courses remain two course results.
        foreach (var selection in new List<string>[] { ["ELA"], ["ESL", "ELA"], [] })
        {
            var selectedResponse = await controller.Run(request with { EnglishProgrammes = selection }, default);
            var selected = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(selectedResponse.Result).Value);
            var onlyEla = selection.Count == 1;
            // Ranking rows count students after averaging their selected course results.
            var ranking = viewKey is "top-students" or "bottom-students";
            Assert.Equal(onlyEla || ranking ? 1 : 2, selected.Total.ScoreCount);
            Assert.Equal(1, selected.Total.StudentCount);
            Assert.Equal(viewKey == "current-course-score" ? (onlyEla ? 100 : 90) : (onlyEla ? 10 : 9), selected.Total.AverageScore);
            if (viewKey.Contains("distribution"))
                Assert.Equal((1, onlyEla ? 0 : 1, 0, 0, 0),
                    (selected.Total.APlus, selected.Total.A, selected.Total.B, selected.Total.C, selected.Total.D));
        }

        // Other subjects still contribute to school-wide views; an explicit stale ELA
        // course selection cannot bypass the policy, including in current-score reports.
        if (viewKey is "subject-grade-average" or "current-course-score" or "assessment-average")
        {
            var schoolResponse = await controller.Run(request with { SubaccountIds = [] }, default);
            var school = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(schoolResponse.Result).Value);
            Assert.Equal(2, school.Total.ScoreCount);
            Assert.Equal(viewKey == "current-course-score" ? 85 : 8.5, school.Total.AverageScore);
            Assert.Contains(school.Rows, row => row.Label.Contains("Math"));
            var elaResponse = await controller.Run(request with { CanvasCourseId = 101 }, default);
            var ela = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(elaResponse.Result).Value);
            Assert.Empty(ela.Rows);
            var includedElaResponse = await controller.Run(request with { CanvasCourseId = 101, EnglishProgrammes = ["ELA"] }, default);
            var includedEla = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(includedElaResponse.Result).Value);
            Assert.Equal(1, includedEla.Total.ScoreCount);
            var mathResponse = await controller.Run(request with { SubaccountIds = [30], EnglishProgrammes = ["ELA"] }, default);
            var math = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(mathResponse.Result).Value);
            Assert.Equal(1, math.Total.ScoreCount);
            Assert.Equal(viewKey == "current-course-score" ? 90 : 9, math.Total.AverageScore);
        }
        if (viewKey == "assessment-average")
        {
            var defaultsResponse = await controller.Run(new ScoreReportQuery
            {
                ViewKey = viewKey, AcademicYear = "2026-2027", Term = "hk1", SubaccountIds = [20]
            }, default);
            var defaults = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(defaultsResponse.Result).Value);
            Assert.Equal(2, defaults.Total.ScoreCount);
            Assert.IsType<BadRequestObjectResult>((await controller.Run(request with { EnglishProgrammes = ["unknown"] }, default)).Result);
            Assert.IsType<BadRequestObjectResult>((await controller.Run(request with { EnglishProgrammes = null! }, default)).Result);
        }
        var optionsResponse = await controller.Options(default);
        var options = Assert.IsType<ScoreReportOptions>(Assert.IsType<OkObjectResult>(optionsResponse.Result).Value);
        Assert.Equal(new[] { "canvas-course-100", "canvas-course-101", "canvas-course-200" }, options.Subjects.Select(option => option.Code).Order());
        Assert.Equal(originalIds, await db.CanvasLeadershipReportAggregates.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).ToListAsync());
        Assert.Equal(5, await db.CanvasDashboardMetricAggregates.CountAsync());
        Assert.Equal(24, (await db.CanvasDashboardAggregateBuilds.AsNoTracking().SingleAsync()).LastCompletedSyncRunId);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
    }

    [MySqlTheory]
    [InlineData("subject-letter-distribution")]
    [InlineData("grade-letter-distribution")]
    [InlineData("class-letter-distribution")]
    [InlineData("class-subject-distribution")]
    public async Task ScoreReportsApplyNewBandsToExistingSnapshotWithoutReaggregation(string viewKey)
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var course = new CanvasCourse
        {
            CanvasCourseId = 100, Name = "Math 5H1", CanvasAccountId = 20, CanvasGradeLevel = "5"
        };
        var section = new CanvasCourseSection { CanvasSectionId = 200, Course = course, Name = "5H1" };
        db.CanvasAccounts.Add(new CanvasAccount { CanvasAccountId = 20, Name = "G5_Math" });
        db.CanvasCourseSections.Add(section);
        // Exercise both the new A/A+ cutoff and the unchanged B/C/D cutoffs,
        // including values that round across each boundary at one decimal.
        double[] scores = [0, 4.94, 4.95, 5, 6.44, 6.45, 6.5, 7.94,
            7.95, 8, 8.9, 8.94, 8.95, 9, 9.4, 9.5, 10];
        for (var index = 0; index < scores.Length; index++)
        {
            var student = new StudentProfile
            {
                SISUserId = $"band-{index}", FullName = $"Student {index}", GradeLevel = "5", Homeroom = "5H1"
            };
            db.CanvasLeadershipReportAggregates.Add(new CanvasLeadershipReportAggregate
            {
                Student = student, Course = course, AcademicYear = "2026-2027", Term = "hk1",
                AssignmentGroupName = "GHK1", AssessmentCategory = "HS2",
                EarnedPoints = scores[index] * 10, PossiblePoints = 100,
                ScoreSum = scores[index] * 10, ScoreCount = 1, Graded = 1
            });
            db.CanvasEnrollments.Add(new CanvasEnrollment
            {
                CanvasEnrollmentId = index + 1, CanvasUserId = index + 1,
                Course = course, Section = section, Student = student,
                Type = "StudentEnrollment", EnrollmentState = "active"
            });
        }
        var snapshot = new CanvasDashboardAggregateBuild
        {
            SchemaVersion = 5, LastCompletedSyncRunId = 24, CompletedAtUtc = new DateTime(2026, 10, 20)
        };
        db.CanvasDashboardAggregateBuilds.Add(snapshot);
        await db.SaveChangesAsync();
        var originalFacts = await db.CanvasLeadershipReportAggregates.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.EarnedPoints, row.PossiblePoints }).ToListAsync();
        var controller = new ScoreReportsController(db, new UserAccessService(db, new HttpContextAccessor()));
        var response = await controller.Run(new ScoreReportQuery
        {
            ViewKey = viewKey, AcademicYear = "2026-2027", Term = "hk1", Grade = "5",
            SubaccountIds = [20], AssessmentCategory = "HS2",
            CanvasSectionName = viewKey == "class-subject-distribution" ? "5H1" : null
        }, default);
        var result = Assert.IsType<ScoreReportResult>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.True(result.AggregateAvailable);
        var row = Assert.Single(result.Rows);
        Assert.Equal((5, 4, 3, 3, 2), (row.APlus, row.A, row.B, row.C, row.D));
        Assert.Equal(scores.Length, row.ScoreCount);
        Assert.Equal(29.4, row.CdPercent);
        Assert.Equal((5, 4, 3, 3, 2), (result.Total.APlus, result.Total.A, result.Total.B, result.Total.C, result.Total.D));
        Assert.Equal(scores.Length, result.Total.StudentCount);
        Assert.Equal(29.4, result.Total.CdPercent);
        Assert.Equal(originalFacts, await db.CanvasLeadershipReportAggregates.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.EarnedPoints, item.PossiblePoints }).ToListAsync());
        var currentSnapshot = await db.CanvasDashboardAggregateBuilds.AsNoTracking().SingleAsync();
        Assert.Equal(snapshot.Id, currentSnapshot.Id);
        Assert.Equal(snapshot.CompletedAtUtc, currentSnapshot.CompletedAtUtc);
        Assert.Equal(24, currentSnapshot.LastCompletedSyncRunId);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
    }

    [MySqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingCourseUpdatesDoNotWriteSisProfilesWhenStudentLookupsAreReused(bool holdStudentReadLock)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using (var seed = test.Open())
        {
            var student = new StudentProfile
            {
                SISUserId = "test", CanvasUserId = 10, FullName = "SIS name", CanvasGradeLevel = "5"
            };
            seed.StudentProfiles.Add(student);
            seed.CanvasSyncStates.Add(new CanvasSyncState
            {
                SourceKey = "canvas:canvas.test:1", LastSuccessfulWatermarkUtc = new DateTime(2026, 10, 1)
            });
            foreach (var courseId in new long[] { 100, 200 })
            {
                seed.CanvasCourses.Add(new CanvasCourse { CanvasCourseId = courseId, Name = "Existing" });
                seed.CanvasEnrollments.Add(new CanvasEnrollment
                {
                    CanvasEnrollmentId = courseId, CanvasCourseId = courseId, CanvasUserId = 10, Student = student
                });
                for (var i = 1; i <= 64; i++)
                {
                    var assignmentId = courseId * 1000 + i;
                    seed.CanvasAssignments.Add(new CanvasAssignment
                    {
                        CanvasAssignmentId = assignmentId, CanvasCourseId = courseId, PointsPossible = 10
                    });
                    seed.CanvasSubmissions.Add(new CanvasSubmission
                    {
                        CanvasSubmissionId = assignmentId, CanvasAssignmentId = assignmentId,
                        CanvasCourseId = courseId, CanvasUserId = 10, Student = student, Score = 1
                    });
                }
            }
            await seed.SaveChangesAsync();
        }
        var observer = new CourseStudentWriteObserver();
        var options = new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(observer).Options;
        await using var db = new AppDbContext(options);
        await using var reader = new MySqlConnection(db.Database.GetConnectionString());
        await reader.OpenAsync();
        await using var readerTransaction = await reader.BeginTransactionAsync();
        if (holdStudentReadLock)
        {
            // A concurrent report reader may hold shared locks. Course facts only
            // need compatible FK checks; an accidental SIS UPDATE blocks here.
            await using var command = reader.CreateCommand();
            command.Transaction = readerTransaction;
            command.CommandText = "SELECT Id FROM StudentProfiles WHERE SISUserId='test' FOR SHARE";
            await command.ExecuteScalarAsync();
        }
        using var handler = new FakeCanvasHandler(includeFacts: true);
        using var http = new HttpClient(handler);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await new CanvasDirectImportRunner().Run(db, http,
            new CanvasDirectImportRequest(new Uri("https://canvas.test"), "test-token", "1",
                false, true, true, true, [1], "incremental", 120, null), deadline.Token);
        await readerTransaction.RollbackAsync();
        Assert.Equal(2, observer.CourseSaves);
        Assert.Equal(0, observer.StudentWrites);
        Assert.Equal(128, await db.CanvasSubmissions.CountAsync(item => item.Score == 8));
        Assert.Equal("SIS name", (await db.StudentProfiles.SingleAsync()).FullName);
    }

    private sealed class CourseStudentWriteObserver : SaveChangesInterceptor
    {
        public int CourseSaves { get; private set; }
        public int StudentWrites { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken token = default)
        {
            var db = eventData.Context!;
            if (db.ChangeTracker.Entries<CanvasCourse>().Any(entry => entry.State is EntityState.Added or EntityState.Modified))
            {
                CourseSaves++;
                StudentWrites += db.ChangeTracker.Entries<StudentProfile>()
                    .Count(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
            }
            return ValueTask.FromResult(result);
        }
    }

    [MySqlFact]
    public async Task WarehouseLockContentionTimesOutWithoutDisturbingOwnerAndCanBeRetried()
    {
        await using var test = await TestDatabase.CreateAsync(createSchema: false);
        await using var owner = test.Open();
        await using var contender = test.Open();
        await using (var heldLock = await CanvasWarehouseLock.AcquireAsync(owner, default))
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
                CanvasWarehouseLock.AcquireAsync(contender, default, TimeSpan.FromMilliseconds(250)));
            Assert.Contains("Canvas warehouse lock", exception.Message);
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Equal(System.Data.ConnectionState.Closed, contender.Database.GetDbConnection().State);
            await using var command = owner.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT IS_USED_LOCK('olympia_canvas_warehouse') = CONNECTION_ID()";
            Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
        await using (var acquiredAfterRelease = await CanvasWarehouseLock.AcquireAsync(
            contender, default, TimeSpan.FromSeconds(2)))
        {
            Assert.Equal(System.Data.ConnectionState.Open, contender.Database.GetDbConnection().State);
        }
        await AssertWarehouseLockReleased(contender);
    }

    [MySqlFact]
    public async Task WarehouseLockWaitCanBeCancelled()
    {
        await using var test = await TestDatabase.CreateAsync(createSchema: false);
        await using var owner = test.Open();
        await using var contender = test.Open();
        await using var heldLock = await CanvasWarehouseLock.AcquireAsync(owner, default);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CanvasWarehouseLock.AcquireAsync(contender, cancellation.Token));
        Assert.Equal(System.Data.ConnectionState.Closed, contender.Database.GetDbConnection().State);
    }

    [MySqlFact]
    public async Task MigrationOnlyEntrypointRunsWithoutJwtOrWebHostAndCanBeRepeated()
    {
        await using var test = await TestDatabase.CreateAsync(createSchema: false);
        await using var db = test.Open();
        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "OlympiaStudent360.Tests.runtimeconfig.json"),
            "--depsfile", Path.Combine(AppContext.BaseDirectory, "OlympiaStudent360.Tests.deps.json"),
            typeof(AppDbContext).Assembly.Location, "--migrate-database-only"
        }) start.ArgumentList.Add(argument);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Jwt__SigningKey"] = "";
        start.Environment["ConnectionStrings__DefaultConnection"] = db.Database.GetConnectionString();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var process = System.Diagnostics.Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch { process.Kill(entireProcessTree: true); throw; }
            Assert.True(process.ExitCode == 0, await output + await errors);
            Assert.Contains("Backend database migrations completed successfully.", await output);
            Assert.DoesNotContain("Now listening", await output);
        }
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [MySqlTheory]
    [InlineData("20261017145751_PrecomputeCanvasLeadershipReports", "20261015065118_AddErpStudentMeals", 2)]
    [InlineData("20261017152023_TriggerCanvasReportAggregationOnCompletedSync", "20261017145751_PrecomputeCanvasLeadershipReports", 1)]
    [InlineData("20261017161256_PersistCanvasGroupScorePoints", "20261017152023_TriggerCanvasReportAggregationOnCompletedSync", 1)]
    [InlineData("20261017162556_OptimizeCanvasScorePointBackfill", "20261017161256_PersistCanvasGroupScorePoints", 1)]
    [InlineData("20261020200719_TrackCanvasReportCourseChanges", "20261019113745_ReclassifyGrade5AsLowerSecondary", 1)]
    [InlineData("20261020200719_TrackCanvasReportCourseChanges", "20261019113745_ReclassifyGrade5AsLowerSecondary", 3)]
    [InlineData("20261021093911_ClassifyGhkGroupsAsHs2", "20261020200719_TrackCanvasReportCourseChanges", 1)]
    [InlineData("20261028025118_AddCanvasActivityReportingDate", "20261021093911_ClassifyGhkGroupsAsHs2", 1)]
    [InlineData("20261028030256_AddLeadershipActivityReportingDate", "20261028025118_AddCanvasActivityReportingDate", 1)]
    public async Task InterruptedReportMigrationsResumeWithoutDroppingCommittedSchema(
        string interruptedMigration, string previousMigration, int completedOperations)
    {
        await using var test = await TestDatabase.CreateAsync(targetMigration: previousMigration);
        await using var db = test.Open();
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[interruptedMigration], db.Database.ProviderName!);
        var model = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var operation in migration.UpOperations.Take(completedOperations))
            foreach (var command in generator.Generate([operation], model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        // A marker on the existing table proves recovery adopts it rather than
        // dropping/recreating already committed DDL after an App Runner rollback.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE `CanvasLeadershipReportAggregates` COMMENT='preserve-on-recovery'");
        // Seed using the old schema: current entities may include columns that this
        // interrupted migration has not created yet.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO `CanvasSyncRuns`
                (`Id`,`SourceKey`,`Source`,`Workflow`,`Status`,`StartedAtUtc`,`CompletedAtUtc`,`WatermarkUtc`,`WarehouseCreatedAt`)
            VALUES (9999,'test','test','incremental','completed',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
            """);
        if (interruptedMigration.Contains("TriggerCanvas"))
        {
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO `CanvasDashboardAggregateBuilds` (`SchemaVersion`,`CompletedAtUtc`,`CreatedAt`)
                VALUES (4,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
                """);
        }
        await db.MigrateWithRecoveryAsync();
        Assert.Null(db.Database.GetCommandTimeout());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        if (interruptedMigration.Contains("TriggerCanvas"))
            Assert.Equal(9999, await db.CanvasDashboardAggregateBuilds.AsNoTracking()
                .Select(item => item.LastCompletedSyncRunId).SingleAsync());
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT TABLE_COMMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='CanvasLeadershipReportAggregates'";
            Assert.Equal("preserve-on-recovery", await command.ExecuteScalarAsync());
        }
        finally { await db.Database.CloseConnectionAsync(); }
        // An explicit caller timeout is also retained on subsequent initialization.
        db.Database.SetCommandTimeout(37);
        await db.MigrateWithRecoveryAsync();
        Assert.Equal(37, db.Database.GetCommandTimeout());
    }

    [MySqlFact]
    public async Task GhkMigrationReclassifiesPublishedReportsWithoutRebuildingFacts()
    {
        await using var test = await TestDatabase.CreateAsync(
            targetMigration: "20261020200719_TrackCanvasReportCourseChanges");
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "ghk-test", FullName = "Report student" };
        var course = new CanvasCourse { CanvasCourseId = 100, Name = "Report course" };
        db.StudentProfiles.Add(student);
        db.CanvasCourses.Add(course);
        db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild
        {
            SchemaVersion = 5, LastCompletedSyncRunId = 24,
            CompletedAtUtc = new DateTime(2026, 10, 20), PeriodConfigurationHash = "existing-snapshot"
        });
        await db.SaveChangesAsync();
        // Seed the pre-activity-date schema without using the current entity's new column.
        foreach (var group in new[] { "GHK1", "GHK2", " ghk1 ", "GHK10", "HS1" })
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO `CanvasLeadershipReportAggregates`
                    (`StudentProfileId`,`CanvasCourseId`,`AcademicYear`,`Term`,`IsFinalCourseScore`,
                     `AssignmentGroupName`,`AssessmentCategory`,`Submitted`,`Missing`,`Late`,`Graded`,
                     `ScoreSum`,`ScoreCount`,`EarnedPoints`,`PossiblePoints`,`CreatedAt`)
                VALUES ({student.Id},{course.CanvasCourseId},'2026-2027','HK1',0,
                    {group},{(group == "HS1" ? "HS1" : null)},0,0,0,0,75,1,7.5,10,UTC_TIMESTAMP(6))
                """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO `CanvasDashboardMetricAggregates`
                (`StudentProfileId`,`CanvasCourseId`,`AcademicYear`,`Term`,`MetricKey`,`ValueSum`,`SampleCount`,`CreatedAt`)
            VALUES ({student.Id},{course.CanvasCourseId},'2026-2027','HK1',
                {CanvasDashboardAggregateService.SubmissionScoreAverage},75,1,UTC_TIMESTAMP(6))
            """);
        var metricId = await db.CanvasDashboardMetricAggregates.Select(item => item.Id).SingleAsync();
        var reportIds = await db.CanvasLeadershipReportAggregates.OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync();

        await db.MigrateWithRecoveryAsync();
        await db.MigrateWithRecoveryAsync();
        var rows = await db.CanvasLeadershipReportAggregates.AsNoTracking().OrderBy(row => row.Id).ToListAsync();
        Assert.Equal(reportIds, rows.Select(row => row.Id));
        Assert.Equal(new string?[] { "HS2", "HS2", "HS2", null, "HS1" }, rows.Select(row => row.AssessmentCategory));
        Assert.All(rows, row =>
        {
            Assert.Equal(75, row.ScoreSum);
            Assert.Equal(7.5, row.EarnedPoints);
            Assert.Equal(10, row.PossiblePoints);
        });
        Assert.Equal(metricId, (await db.CanvasDashboardMetricAggregates.AsNoTracking().SingleAsync()).Id);
        var snapshot = await db.CanvasDashboardAggregateBuilds.AsNoTracking().SingleAsync();
        Assert.Equal(5, snapshot.SchemaVersion);
        Assert.Equal(24, snapshot.LastCompletedSyncRunId);
        Assert.Equal("existing-snapshot", snapshot.PeriodConfigurationHash);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
    }

    [MySqlFact]
    public async Task ExplicitSessionDoesNotReplayCommandsAndTransactionRollsBack()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        await using (var heldLock = await CanvasWarehouseLock.AcquireAsync(db, default))
        {
            var strategy = db.Database.CreateExecutionStrategy();
            Assert.False(strategy.RetriesOnFailure);
            var attempts = 0;
            await Assert.ThrowsAsync<TimeoutException>(() => strategy.ExecuteAsync(() =>
            {
                attempts++;
                return Task.FromException(new TimeoutException());
            }));
            Assert.Equal(1, attempts);

            await using var transaction = await db.Database.BeginTransactionAsync();
            db.StudentProfiles.Add(new StudentProfile { SISUserId = "rollback", FullName = "Test" });
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.Empty(await db.StudentProfiles.ToListAsync());
    }

    [MySqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanvasRetryUsesFreshContextReplaysUpsertsAndAdvancesWatermarkOnlyOnSuccess(bool killConnection)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var fault = new FailCourseReadOnce(killConnection);
        var options = new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(fault).Options;
        await using var db = new AppDbContext(options);
        var oldWatermark = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        db.StudentProfiles.Add(new StudentProfile { SISUserId = "test", CanvasUserId = 10, FullName = "Test" });
        db.CanvasSyncStates.Add(new CanvasSyncState
        {
            SourceKey = "canvas:canvas.test:1", LastSuccessfulWatermarkUtc = oldWatermark
        });
        await db.SaveChangesAsync();
        using var handler = new FakeCanvasHandler();
        using var http = new HttpClient(handler);
        var result = await new CanvasDirectImportRunner().Run(db, http,
            new CanvasDirectImportRequest(new Uri("https://canvas.test"), "test-token", "1",
                false, true, true, true, [1], "incremental", 120, null), default);

        db.ChangeTracker.Clear();
        var runs = await db.CanvasSyncRuns.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(new[] { "failed", "completed" }, runs.Select(item => item.Status));
        Assert.Equal(oldWatermark.AddMinutes(-120), runs[1].WindowStartUtc);
        Assert.Equal(2, fault.ContextIds.Distinct().Count());
        Assert.Equal(2, await db.CanvasCourses.CountAsync());
        Assert.Equal(2, await db.CanvasEnrollments.CountAsync());
        var state = await db.CanvasSyncStates.SingleAsync();
        Assert.Equal(result.WatermarkUtc, DateTime.SpecifyKind(state.LastSuccessfulWatermarkUtc!.Value, DateTimeKind.Utc),
            TimeSpan.FromMilliseconds(1));
        Assert.Equal("completed", state.LastStatus);
        await AssertWarehouseLockReleased(db);
    }

    [MySqlFact]
    public async Task CourseBatchesProduceCorrectMetricsAndKeepPreviousSnapshotReadable()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "test", FullName = "Test" };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();
        var now = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        foreach (var courseId in new long[] { 100, 200 })
        {
            db.CanvasCourses.Add(new CanvasCourse { CanvasCourseId = courseId, EnrollmentTermName = "2026-27", EndAt = now });
            db.CanvasAssignments.Add(new CanvasAssignment
            {
                CanvasAssignmentId = courseId, CanvasCourseId = courseId,
                AssignmentGroupName = "Test", PointsPossible = 10, DueAt = now
            });
            db.CanvasEnrollments.Add(new CanvasEnrollment
            {
                CanvasEnrollmentId = courseId, CanvasCourseId = courseId, CanvasUserId = 10,
                StudentProfileId = student.Id
            });
            db.CanvasCourseScores.Add(new CanvasCourseScore
            {
                WarehouseScoreFactId = courseId, CanvasEnrollmentId = courseId,
                StudentProfileId = student.Id, CurrentScore = 80, FinalScore = 90, CurrentPoints = 8,
                GradingPeriodEndDate = now
            });
            db.CanvasSubmissions.Add(new CanvasSubmission
            {
                CanvasSubmissionId = courseId, CanvasCourseId = courseId, CanvasAssignmentId = courseId,
                CanvasUserId = 10, StudentProfileId = student.Id, WorkflowState = "graded",
                Score = 8, SubmittedAt = now, GradedAt = now, Late = true
            });
        }
        db.CanvasSyncRuns.Add(CompletedRun(now));
        await db.SaveChangesAsync();
        Assert.False(await CanvasDashboardAggregateService.HasReadableSnapshotAsync(db));
        var count = await new CanvasDashboardAggregateService(db).RebuildAsync(default);
        Assert.Null(db.Database.GetCommandTimeout());
        Assert.Equal(count, await db.CanvasDashboardMetricAggregates.CountAsync());
        var metrics = await db.CanvasDashboardMetricAggregates.ToListAsync();
        foreach (var courseId in new long[] { 100, 200 })
        {
            var course = metrics.Where(item => item.CanvasCourseId == courseId).ToList();
            Assert.Equal(7, course.Count);
            Assert.Equal(80, course.Single(item => item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage).ValueSum);
            Assert.Equal(90, course.Single(item => item.MetricKey == CanvasDashboardAggregateService.FinalScoreAverage).ValueSum);
            Assert.Equal(1, course.Single(item => item.MetricKey == CanvasDashboardAggregateService.AssignmentsLate).ValueSum);
        }
        db.CanvasSyncRuns.Add(CompletedRun(now.AddDays(1)));
        await db.SaveChangesAsync();
        db.Database.SetCommandTimeout(47);
        Assert.True(await CanvasDashboardAggregateService.HasReadableSnapshotAsync(db));
        Assert.NotNull(await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default));
        Assert.Null(await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default));
        Assert.Equal(47, db.Database.GetCommandTimeout());
        await AssertWarehouseLockReleased(db);
    }

    [MySqlFact]
    public async Task IncrementalReportsPreserveUnchangedHistoryAndSkipIdenticalImports()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedHistoricalCourseAsync(db);
        await ImportReportFactsAsync(db);
        var original = await db.CanvasDashboardMetricAggregates.AsNoTracking().ToListAsync();
        var unchangedIds = original.Where(item => item.CanvasCourseId != 100).Select(item => item.Id).Order().ToArray();
        Assert.Contains(original, item => item.AcademicYear == "2025-2026");
        await ImportReportFactsAsync(db, 9);
        Assert.Equal(unchangedIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId != 100).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
        Assert.All(await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId == 100 && item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage)
            .ToListAsync(), item => Assert.Equal(90, item.ValueSum));
        var leadership = await db.CanvasLeadershipReportAggregates.AsNoTracking()
            .SingleAsync(item => item.CanvasCourseId == 100);
        Assert.Equal(576, leadership.EarnedPoints);
        Assert.Equal(640, leadership.PossiblePoints);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
        var refreshedIds = await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        await ImportReportFactsAsync(db, 9);
        Assert.Equal(refreshedIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
        Assert.Equal(await db.CanvasSyncRuns.MaxAsync(item => item.Id),
            (await db.CanvasDashboardAggregateBuilds.AsNoTracking().SingleAsync()).LastCompletedSyncRunId);

        // The first deployment adopts a compatible snapshot instead of rebuilding history.
        await db.CanvasDashboardAggregateBuilds.ExecuteUpdateAsync(setters =>
            setters.SetProperty(item => item.PeriodConfigurationHash, (string?)null));
        Assert.Null(await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default));
        Assert.Equal(refreshedIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());

        // Configured academic periods affect historical bucketing even without a new import.
        db.AcademicPeriods.Add(new AcademicPeriod
        {
            AcademicYear = "2026-2027", Term = "hk2",
            StartDate = new DateTime(2026, 10, 1), EndDate = new DateTime(2026, 11, 1)
        });
        await db.SaveChangesAsync();
        Assert.NotNull(await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default));
        Assert.All(await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId == 100).ToListAsync(), item => Assert.Equal("hk2", item.Term));
        Assert.Null(await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default));
    }

    [MySqlFact]
    public async Task FailedIncrementalPublicationKeepsReadableSnapshotAndDurableCourseChanges()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedHistoricalCourseAsync(db);
        await ImportReportFactsAsync(db);
        var originalIds = await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        var historicalIds = await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId == 3000).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        var fault = new FailMetricInsertOnce();
        var options = new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(fault).Options;
        await using (var failingDb = new AppDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => ImportReportFactsAsync(failingDb, 9));
        Assert.True(await CanvasDashboardAggregateService.HasReadableSnapshotAsync(db));
        Assert.Equal(originalIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
        Assert.Equal(new long[] { 100 }, await db.CanvasReportCourseChanges.AsNoTracking()
            .Select(item => item.CanvasCourseId).ToArrayAsync());
        Assert.All(await db.CanvasSyncRuns.AsNoTracking().ToListAsync(), item => Assert.Equal("completed", item.Status));
        await new CanvasDashboardAggregateService(db).RefreshIfNeededAsync(default);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
        Assert.All(await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId == 100 && item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage)
            .ToListAsync(), item => Assert.Equal(90, item.ValueSum));
        Assert.Equal(historicalIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId == 3000).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
    }

    private static async Task SeedHistoricalCourseAsync(AppDbContext db)
    {
        var student = new StudentProfile { SISUserId = "test", CanvasUserId = 10, FullName = "Test" };
        db.StudentProfiles.Add(student);
        db.CanvasSyncStates.Add(new CanvasSyncState
        { SourceKey = "canvas:canvas.test:1", LastSuccessfulWatermarkUtc = new DateTime(2026, 10, 1) });
        db.CanvasCourses.Add(new CanvasCourse { CanvasCourseId = 3000, Name = "Historical" });
        db.CanvasAssignments.Add(new CanvasAssignment
        { CanvasAssignmentId = 3000, CanvasCourseId = 3000, PointsPossible = 10, Published = true });
        db.CanvasSubmissions.Add(new CanvasSubmission
        {
            CanvasSubmissionId = 3000, CanvasAssignmentId = 3000, CanvasCourseId = 3000,
            Student = student, CanvasUserId = 10, Score = 4, WorkflowState = "graded",
            SubmittedAt = new DateTime(2025, 10, 10), GradedAt = new DateTime(2025, 10, 10)
        });
        await db.SaveChangesAsync();
    }

    [MySqlFact]
    public async Task HistoricalIdentityRelinksRefreshOnlyTheirCourseOutsideTheImportWindow()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedHistoricalCourseAsync(db);
        await db.CanvasSubmissions.Where(item => item.CanvasCourseId == 3000)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.StudentProfileId, (int?)null)
                .SetProperty(item => item.CanvasUserId, 88L));
        await ImportReportFactsAsync(db);
        Assert.False(await db.CanvasDashboardMetricAggregates.AnyAsync(item => item.CanvasCourseId == 3000));
        var currentIds = await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        db.StudentProfiles.Add(new StudentProfile { SISUserId = "new-student", CanvasUserId = 88, FullName = "Historical student" });
        await db.SaveChangesAsync();
        await ImportReportFactsAsync(db);
        Assert.Equal(currentIds, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId != 3000).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
        Assert.Equal(40, (await db.CanvasDashboardMetricAggregates.AsNoTracking().SingleAsync(item =>
            item.CanvasCourseId == 3000 && item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage)).ValueSum);
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
    }

    private static async Task ImportReportFactsAsync(AppDbContext db, double firstCourseScore = 8,
        bool fixedActivity = false, int? onlySubmission = null, bool omitLastAssignment = false,
        bool deleteFirstCourse = false)
    {
        using var handler = new FakeCanvasHandler(includeFacts: true, firstCourseScore,
            fixedActivity, onlySubmission, omitLastAssignment, deleteFirstCourse);
        using var http = new HttpClient(handler);
        await new CanvasDirectImportRunner().Run(db, http,
            new CanvasDirectImportRequest(new Uri("https://canvas.test"), "test-token", "1",
                false, true, true, true, [1], "incremental", 120, null), default);
    }

    [MySqlFact]
    public async Task IncrementalImportSkipsUnchangedWritesLoadsOnlyIncomingFactsAndPreservesInactiveHistory()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedHistoricalCourseAsync(db);
        await ImportReportFactsAsync(db, fixedActivity: true);
        var noOp = new WarehouseReadWriteObserver();
        await using (var repeat = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options)
            .AddInterceptors(noOp).Options))
            await ImportReportFactsAsync(repeat, fixedActivity: true);
        Assert.Equal(0, noOp.CanvasWrites);

        // The partial submission payload must not wipe older facts, and a removed
        // assignment with inactive-student history must survive source pruning.
        await db.CanvasSubmissions.Where(item => item.CanvasSubmissionId == 100064)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.StudentProfileId, (int?)null)
                .SetProperty(item => item.CanvasUserId, 88L));
        var sparse = new WarehouseReadWriteObserver();
        await using (var incremental = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options)
            .AddInterceptors(sparse).Options))
            await ImportReportFactsAsync(incremental, 9, fixedActivity: true, onlySubmission: 1, omitLastAssignment: true);
        Assert.Equal(2, sparse.SubmissionsLoaded);
        Assert.Equal(128, await db.CanvasSubmissions.CountAsync()); // 129 before one active assignment was deleted
        Assert.True(await db.CanvasAssignments.AnyAsync(item => item.CanvasAssignmentId == 100064));
        Assert.False(await db.CanvasAssignments.AnyAsync(item => item.CanvasAssignmentId == 200064));
        Assert.Equal(9, (await db.CanvasSubmissions.AsNoTracking().SingleAsync(item => item.CanvasSubmissionId == 100001)).Score);
        Assert.Equal(8, (await db.CanvasSubmissions.AsNoTracking().SingleAsync(item => item.CanvasSubmissionId == 100002)).Score);
    }

    private sealed class WarehouseReadWriteObserver : SaveChangesInterceptor, IMaterializationInterceptor
    {
        public int CanvasWrites { get; private set; }
        public int SubmissionsLoaded { get; private set; }
        public object InitializedInstance(MaterializationInterceptionData data, object entity)
        {
            if (entity is CanvasSubmission) SubmissionsLoaded++;
            return entity;
        }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken token = default)
        {
            CanvasWrites += eventData.Context!.ChangeTracker.Entries().Count(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                entry.Entity is CanvasAccount or CanvasUser or CanvasCourse or CanvasCourseSection
                    or CanvasAssignment or CanvasEnrollment or CanvasCourseScore or CanvasSubmission);
            return ValueTask.FromResult(result);
        }
    }

    [MySqlFact]
    public async Task DeletedCourseInvalidationIsConsumedWithoutRebuildingOtherSchoolYears()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedHistoricalCourseAsync(db);
        await ImportReportFactsAsync(db, fixedActivity: true);
        var unaffected = await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .Where(item => item.CanvasCourseId != 100).OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync();
        await ImportReportFactsAsync(db, fixedActivity: true, deleteFirstCourse: true);
        Assert.False(await db.CanvasCourses.AnyAsync(item => item.CanvasCourseId == 100));
        Assert.False(await db.CanvasDashboardMetricAggregates.AnyAsync(item => item.CanvasCourseId == 100));
        Assert.Equal(unaffected, await db.CanvasDashboardMetricAggregates.AsNoTracking()
            .OrderBy(item => item.Id).Select(item => item.Id).ToArrayAsync());
        Assert.Empty(await db.CanvasReportCourseChanges.ToListAsync());
    }

    private sealed class FailMetricInsertOnce : DbCommandInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!failed && command.CommandText.StartsWith("INSERT INTO `CanvasDashboardMetricAggregates`"))
            {
                failed = true;
                return ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Injected bulk publication failure"));
            }
            return ValueTask.FromResult(result);
        }
    }

    [MySqlTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReportFailureDoesNotReplayOrMislabelCommittedImport(bool transient)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var fault = new FailReportWrite(transient);
        var options = new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(fault).Options;
        await using var db = new AppDbContext(options);
        var oldWatermark = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        db.StudentProfiles.Add(new StudentProfile { SISUserId = "test", CanvasUserId = 10, FullName = "Test" });
        db.CanvasSyncStates.Add(new CanvasSyncState
        {
            SourceKey = "canvas:canvas.test:1", LastSuccessfulWatermarkUtc = oldWatermark
        });
        await db.SaveChangesAsync();
        using var handler = new FakeCanvasHandler();
        using var http = new HttpClient(handler);
        var import = new CanvasDirectImportRunner().Run(db, http,
            new CanvasDirectImportRequest(new Uri("https://canvas.test"), "test-token", "1",
                false, true, true, true, [1], "incremental", 120, null), default);
        if (transient)
            await import;
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => import);
            Assert.Contains("--refresh-dashboard-aggregates-only", error.Message);
        }
        db.ChangeTracker.Clear();
        Assert.Equal("completed", (await db.CanvasSyncRuns.SingleAsync()).Status);
        var state = await db.CanvasSyncStates.SingleAsync();
        Assert.Equal("completed", state.LastStatus);
        Assert.True(state.LastSuccessfulWatermarkUtc > oldWatermark);
        Assert.Equal(1, handler.CourseListRequests);
        Assert.Equal(transient ? 2 : 1, fault.ContextIds.Distinct().Count());
        await AssertWarehouseLockReleased(db);
    }

    private sealed class FailReportWrite(bool transient) : DbCommandInterceptor
    {
        public List<Guid> ContextIds { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DELETE") && command.CommandText.Contains("CanvasLeadershipReportAggregates"))
            {
                ContextIds.Add(eventData.Context!.ContextId.InstanceId);
                if (ContextIds.Count == 1)
                    return ValueTask.FromException<InterceptionResult<int>>(transient
                        ? new TimeoutException("Injected transient report failure")
                        : new InvalidOperationException("Injected permanent report failure"));
            }
            return ValueTask.FromResult(result);
        }
    }

    private static CanvasSyncRun CompletedRun(DateTime now) => new()
    {
        SourceKey = "test", Source = "test", Workflow = "incremental", Status = "completed",
        StartedAtUtc = now, CompletedAtUtc = now, WatermarkUtc = now
    };

    private static async Task AssertWarehouseLockReleased(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT IS_USED_LOCK('olympia_canvas_warehouse')";
            Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private sealed class FailCourseReadOnce(bool killConnection) : DbCommandInterceptor
    {
        private bool failed;
        public List<Guid> ContextIds { get; } = [];
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM `CanvasCourses` AS") && command.CommandText.Contains("WHERE"))
            {
                ContextIds.Add(eventData.Context!.ContextId.InstanceId);
                // Let the first course commit before failing the next one. A retry must
                // update that first course without introducing duplicate facts.
                if (!failed && ContextIds.Count == 2)
                {
                    failed = true;
                    if (!killConnection) throw new TimeoutException("Injected transient database failure");
                    // Exercise the real connector exception and session-lock loss too.
                    await using var killer = new MySqlConnection(Environment.GetEnvironmentVariable("OLYMPIA_TEST_MYSQL"));
                    await killer.OpenAsync(cancellationToken);
                    await using var kill = killer.CreateCommand();
                    kill.CommandText = $"KILL CONNECTION {((MySqlConnection)command.Connection!).ServerThread}";
                    await kill.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            return result;
        }
    }

    private sealed class FakeCanvasHandler(bool includeFacts = false, double firstCourseScore = 8,
        bool fixedActivity = false, int? onlySubmission = null, bool omitLastAssignment = false,
        bool deleteFirstCourse = false) : HttpMessageHandler
    {
        public int CourseListRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/accounts/1/courses") CourseListRequests++;
            var body = path switch
            {
                "/api/v1/accounts/1" => "{\"id\":1,\"name\":\"Grade 5\"}",
                "/api/v1/accounts/1/sub_accounts" => "[]",
                "/api/v1/accounts/1/users" => "[{\"id\":10,\"sis_user_id\":\"test\"}]",
                "/api/v1/accounts/1/courses" => System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { id = 100, name = "First", workflow_state = deleteFirstCourse ? "deleted" : "available" },
                    new { id = 200, name = "Second", workflow_state = "available" }
                }),
                _ when path.EndsWith("/enrollments") => System.Text.Json.JsonSerializer.Serialize(new[] { new
                {
                    id = long.Parse(path.Split('/')[4]), user_id = 10, type = "StudentEnrollment",
                    enrollment_state = "active", last_activity_at = fixedActivity ? new DateTime(2026, 10, 10) : DateTime.UtcNow
                } }),
                _ when path.EndsWith("/grading_periods") => "{\"grading_periods\":[]}",
                _ when includeFacts && path.EndsWith("/assignments") => System.Text.Json.JsonSerializer.Serialize(
                    Enumerable.Range(1, omitLastAssignment ? 63 : 64).Select(i => new { id = long.Parse(path.Split('/')[4]) * 1000 + i, points_possible = 10, published = true })),
                _ when includeFacts && path.EndsWith("/students/submissions") => System.Text.Json.JsonSerializer.Serialize(
                    (onlySubmission is { } index ? [index] : Enumerable.Range(1, 64)).Select(i => new
                    {
                        id = long.Parse(path.Split('/')[4]) * 1000 + i,
                        assignment_id = long.Parse(path.Split('/')[4]) * 1000 + i, user_id = 10,
                        score = path.Split('/')[4] == "100" ? firstCourseScore : 8,
                        workflow_state = "graded", submitted_at = "2026-10-10T00:00:00Z", graded_at = "2026-10-10T00:00:00Z"
                    })),
                _ => "[]"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        public required DbContextOptions<AppDbContext> Options { get; init; }
        public AppDbContext Open() => new(Options);
        public static async Task<TestDatabase> CreateAsync(
            bool migrate = false, string? targetMigration = null, bool createSchema = true)
        {
            var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("OLYMPIA_TEST_MYSQL"))
            { Database = "olympia_test_" + Guid.NewGuid().ToString("N") };
            var test = new TestDatabase
            {
                Options = new DbContextOptionsBuilder<AppDbContext>().UseResilientMySql(connection.ConnectionString).Options
            };
            var databaseName = connection.Database;
            connection.Database = "";
            await using (var admin = new MySqlConnection(connection.ConnectionString))
            {
                await admin.OpenAsync();
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE `{databaseName}`";
                await create.ExecuteNonQueryAsync();
            }
            await using var db = test.Open();
            if (!createSchema) return test;
            if (targetMigration is not null) await db.GetService<IMigrator>().MigrateAsync(targetMigration);
            else if (migrate) await db.MigrateWithRecoveryAsync();
            else await db.Database.EnsureCreatedAsync();
            return test;
        }
        public async ValueTask DisposeAsync()
        {
            await using var db = Open();
            await db.Database.OpenConnectionAsync();
            try
            {
                var databaseName = db.Database.GetDbConnection().Database;
                await using var drop = db.Database.GetDbConnection().CreateCommand();
                drop.CommandText = $"DROP DATABASE `{databaseName.Replace("`", "``")}`";
                await drop.ExecuteNonQueryAsync();
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
