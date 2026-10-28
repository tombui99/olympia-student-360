using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task RemainingReportsKeepSummariesDetailsAndSupportListsInsideSelectedDates()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "dated-student", FullName = "Dated student", GradeLevel = "5", Homeroom = "5H1" };
        var olderStudent = new StudentProfile { SISUserId = "older-student", FullName = "Earlier signal", GradeLevel = "5", Homeroom = "5H1" };
        db.StudentProfiles.AddRange(student, olderStudent);
        var dates = new[] { new DateTime(2026, 10, 19), new DateTime(2026, 10, 26), new DateTime(2026, 11, 1), new DateTime(2026, 11, 2) };
        foreach (var date in dates)
        {
            db.StudentBehaviours.Add(new StudentBehaviour
            {
                Student = student, IncidentDate = date, AcademicYear = "2026-2027", Term = "hk1",
                BehaviourType = "Đi học muộn", ClassificationCategory = "Concerning"
            });
            db.StudentProjects.Add(new StudentProject
            {
                Student = student, ProjectName = "Dated project", Status = "In progress", Subject = "Math",
                ActivityDate = date.Day == 26 ? null : date, CreatedAt = date, AcademicYear = "2026-2027", Term = "hk1"
            });
            db.StudentMentalHealthRecords.Add(new StudentMentalHealthRecord
            {
                Student = student, RecordDate = date, AcademicYear = "2026-2027", ConcernType = "Support",
                RiskLevel = "High", RecordedByUserId = "test", RecordedByName = "Test"
            });
            db.StudentBusAttendanceRecords.Add(new StudentBusAttendanceRecord
            {
                Student = student, AttendanceDate = date, AcademicYear = "2026-2027", Term = "hk1",
                TripDirection = "To school", ViolationType = "Late"
            });
            db.StudentHealthVisits.Add(new StudentHealthVisit
            {
                Student = student, ExamTime = date.AddHours(9), FullName = student.FullName, ClassName = "5H1", Diagnosis = "Cold"
            });
        }
        db.StudentBehaviours.Add(new StudentBehaviour
        {
            Student = olderStudent, IncidentDate = dates[0], AcademicYear = "2026-2027", Term = "hk1",
            BehaviourType = "Đi học muộn", ClassificationCategory = "Concerning"
        });
        await db.SaveChangesAsync();
        var periods = new CanvasReportingPeriodService(db);
        await new ImportedReportAggregateService(db, periods).RebuildAsync();
        var access = new UserAccessService(db, new HttpContextAccessor());
        var behaviour = new BehaviourReportsController(db, access);
        var summary = Assert.IsType<BehaviourReportSummaryDto>(Assert.IsType<OkObjectResult>(
            (await behaviour.Summary("2026-2027", "all", 10, "2026-10-26")).Result).Value);
        Assert.Equal(1, summary.TotalRecords);
        var details = Assert.IsType<BehaviourReportDetailsPageDto>(Assert.IsType<OkObjectResult>(
            (await behaviour.Details(academicYear: "2026-2027", pageSize: 1000, month: 10, weekStart: "2026-10-26")).Result).Value);
        Assert.Equal(1, details.TotalCount);
        Assert.Equal(dates[1], Assert.Single(details.Items).IncidentDate);

        var supplemental = new SupplementalReportsController(db, access);
        var records = new ModuleRecordsController(db, access, new StudentRecordFieldOptionService(db));
        foreach (var (module, field) in new[] { ("student-projects", "status"), ("student-mental-health", "risk_level"), ("student-bus-attendance", "violation_type") })
        {
            var aggregate = Assert.IsType<OkObjectResult>(await supplemental.Summary(module, field, "2026-2027", "all", 10, "2026-10-26"));
            var json = System.Text.Json.JsonSerializer.SerializeToElement(aggregate.Value);
            Assert.Equal(1, json.GetProperty("TotalCount").GetInt32());
            var page = Assert.IsType<ModuleRecordsPageDto>(Assert.IsType<OkObjectResult>((await records.List(module,
                academicYear: "2026-2027", month: 10, weekStart: "2026-10-26", export: true)).Result).Value);
            Assert.Equal(1, page.TotalCount);
            Assert.Single(page.Items);
            var november = Assert.IsType<ModuleRecordsPageDto>(Assert.IsType<OkObjectResult>((await records.List(module,
                academicYear: "2026-2027", month: 11, weekStart: "2026-10-26")).Result).Value);
            Assert.Equal(1, november.TotalCount);
        }

        var health = new HealthReportsController(db, access);
        foreach (var report in new[] { "diagnoses", "frequent-students", "student-visits" })
        foreach (var grouping in new[] { "month", "week", "year" })
        {
            var page = Assert.IsType<HealthReportPageDto>(Assert.IsType<OkObjectResult>((await health.Summary(report, grouping,
                "2026-2027", "all", month: 10, weekStart: "2026-10-26", export: true)).Result).Value);
            Assert.Equal(1, page.TotalVisits);
            var row = Assert.Single(page.Items);
            var visits = Assert.IsType<HealthVisitReportDetailsPageDto>(Assert.IsType<OkObjectResult>((await health.Details(
                row.PeriodStart, row.PeriodEnd, academicYear: "2026-2027", month: 10, weekStart: "2026-10-26")).Result).Value);
            Assert.Equal(1, visits.TotalCount);
            Assert.Equal(dates[1].AddHours(9), Assert.Single(visits.Items).ExamTime);
        }

        await db.StudentRiskSnapshots.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.RiskLevelWithBus, "High").SetProperty(item => item.RiskLevelWithoutBus, "High"));
        var students = new StudentsController(db, periods, access, new StudentRecordFieldOptionService(db));
        var all = Assert.IsType<AtRiskStudentsPageDto>(Assert.IsType<OkObjectResult>((await students.GetAtRiskStudents(
            academicYear: "2026-2027", term: "all")).Result).Value);
        Assert.Equal(2, all.TotalCount);
        var selected = Assert.IsType<AtRiskStudentsPageDto>(Assert.IsType<OkObjectResult>((await students.GetAtRiskStudents(
            academicYear: "2026-2027", term: "all", month: 10, weekStart: "2026-10-26", export: true)).Result).Value);
        Assert.Equal(student.Id, Assert.Single(selected.Items).Id);
        Assert.Equal("High", selected.Items[0].RiskLevel);

        // A teacher without health/bus access must not learn about those alerts through this filter.
        foreach (var source in new[] { "student-mental-health", "student-bus-attendance" })
            db.ImportedReportAggregates.Add(new ImportedReportAggregate
            {
                Source = source, StudentProfileId = olderStudent.Id, StudentKey = $"id:{olderStudent.Id}",
                StudentName = olderStudent.FullName, EventDay = dates[1], LatestEvent = dates[1],
                AcademicYear = "2026-2027", Term = "hk1", RecordCount = 1, AttentionCount = 1
            });
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        http.Items[UserAccessService.HttpContextItemKey] = new UserAccessSnapshot("test", "Teacher",
            new HashSet<string> { TeacherAccessScopes.Academic }, [], []);
        var restricted = new StudentsController(db, periods,
            new UserAccessService(db, new HttpContextAccessor { HttpContext = http }), new StudentRecordFieldOptionService(db));
        var scoped = Assert.IsType<AtRiskStudentsPageDto>(Assert.IsType<OkObjectResult>((await restricted.GetAtRiskStudents(
            academicYear: "2026-2027", term: "all", month: 10, weekStart: "2026-10-26")).Result).Value);
        Assert.Equal(student.Id, Assert.Single(scoped.Items).Id);
    }
}
