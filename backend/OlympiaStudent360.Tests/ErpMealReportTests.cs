using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task MealReportsUseSchoolDefinitionsAndStudentPeriodQuantities()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var first = new StudentProfile { SISUserId = "meal-a", FullName = "Student A", GradeLevel = "5", Homeroom = "5H1" };
        var second = new StudentProfile { SISUserId = "meal-b", FullName = "Student B", GradeLevel = "5", Homeroom = "5H1" };
        db.StudentProfiles.AddRange(first, second);
        await db.SaveChangesAsync();
        var day = new DateTime(2026, 9, 30);
        void Purchase(StudentProfile student, DateTime date, string? code, int quantity) => db.ErpCanteenPurchases.Add(new()
        {
            StudentProfileId = student.Id, ErpStudentId = student.SISUserId!, PurchaseDate = date,
            ProductCode = code, Quantity = quantity, SourceRecordKey = Guid.NewGuid().ToString(), MatchStatus = "matched"
        });
        Purchase(first, day, "CF_DRINK", 12);
        Purchase(first, day, null, 2);
        Purchase(first, day.AddDays(1), "FOOD_OTHER", 4);
        Purchase(first, day.AddDays(5), "OTHER_ITEM", 2);
        for (var i = 0; i < 5; i++) Purchase(second, day, "CFS_ITEM", 1);
        void Status(DateTime date, string meal, string ticket, string feed = ErpMealFeeds.MissingTicket) => db.ErpMealStatuses.Add(new()
        {
            StudentProfileId = first.Id, ErpStudentId = first.SISUserId!, MealDate = date,
            Meal = meal, OrderStatus = "Có đặt ăn", TicketStatus = ticket, Feed = feed,
            SourceRecordKey = Guid.NewGuid().ToString(), MatchStatus = "matched"
        });
        Status(day, "Sáng", "Không lấy phiếu ăn");
        Status(day, "Sáng", "Không lấy phiếu ăn");
        Status(day.AddDays(1), "Trưa", "Không lấy phiếu ăn");
        Status(day.AddDays(2), "Trưa", "Đã lấy phiếu ăn");
        Status(day.AddDays(3), "Trưa", "Không lấy phiếu ăn", ErpMealFeeds.MissingOrder);
        await db.SaveChangesAsync();
        await new ErpMealReportAggregateService(db).RebuildAsync();
        var controller = new ErpMealReportsController(db, new UserAccessService(db, new HttpContextAccessor()));
        async Task<ErpMealReportPageDto> Summary(string report, string grouping = "student", DateTime? from = null, DateTime? to = null, int pageSize = 1000, int page = 1) =>
            Assert.IsType<ErpMealReportPageDto>(Assert.IsType<OkObjectResult>((await controller.Summary(
                report, grouping, from, to, pageSize: pageSize, page: page, export: true)).Result).Value);
        async Task<ErpMealReportDetailsPageDto> Details(string report, string grouping, string key, DateTime? from = null, DateTime? to = null) =>
            Assert.IsType<ErpMealReportDetailsPageDto>(Assert.IsType<OkObjectResult>((await controller.Details(
                report, grouping, key, from, to, pageSize: 100)).Result).Value);

        foreach (var group in new[] { "student", "week", "month", "grade", "class" })
        {
            var missed = await Summary("ordered-not-eaten", group);
            var noTicket = await Summary("ordered-no-ticket", group);
            Assert.Equal(2, missed.TotalEvents);
            Assert.Equal(noTicket.TotalEvents, missed.TotalEvents);
            Assert.Equal(noTicket.Items.Select(x => (x.Key, x.Count)), missed.Items.Select(x => (x.Key, x.Count)));
            foreach (var row in missed.Items)
            {
                var detail = await Details("ordered-not-eaten", group, row.Key);
                Assert.Equal(row.Count, detail.TotalCount);
                Assert.All(detail.Items, item => Assert.Equal("Không lấy phiếu ăn", item.TicketStatus));
            }
        }
        var coffee = await Summary("coffee-frequency");
        Assert.Equal(25, coffee.TotalUnits);
        Assert.Equal(2, coffee.TotalStudents);
        Assert.Equal(first.Id, coffee.Items[0].StudentProfileId);
        Assert.Equal(20, coffee.Items[0].Units);
        Assert.True(coffee.Items[0].Count < coffee.Items[1].Count, "Quantity ranking must differ from line-count ranking.");
        foreach (var (group, count) in new[] { ("student-day", 4), ("student-week", 3), ("student-month", 3) })
        {
            var result = await Summary("coffee-frequency", group);
            Assert.Equal(count, result.TotalCount);
            Assert.Equal(25, result.Items.Sum(x => x.Units));
            foreach (var row in result.Items)
            {
                Assert.NotNull(row.StudentProfileId);
                var detail = await Details("coffee-frequency", group, row.Key);
                Assert.Equal(row.Units, detail.Items.Sum(x => x.Quantity));
                Assert.All(detail.Items, item =>
                {
                    Assert.Equal(row.StudentProfileId, item.StudentProfileId);
                    Assert.Null(item.ProductCode);
                    Assert.Null(item.ProductName);
                });
                Assert.Equal(detail.Items.Select(x => (x.ErpStudentId, x.EventDay)).Distinct().Count(), detail.TotalCount);
            }
            var next = await Summary("coffee-frequency", group, pageSize: 1, page: 2);
            Assert.Equal(result.Items[1].Key, Assert.Single(next.Items).Key);
        }
        var clipped = await Summary("coffee-frequency", "student-week", day.AddDays(1), day.AddDays(1));
        var clippedRow = Assert.Single(clipped.Items);
        Assert.Equal(4, clippedRow.Units);
        var clippedDetails = await Details("coffee-frequency", "student-week", clippedRow.Key, day.AddDays(1), day.AddDays(1));
        Assert.Equal(day.AddDays(1), Assert.Single(clippedDetails.Items).EventDay);
        Assert.IsType<BadRequestObjectResult>((await controller.Details("coffee-frequency", "student-week", "broken")).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.Summary("no-order", "student-day")).Result);

        var http = new DefaultHttpContext();
        http.Items[UserAccessService.HttpContextItemKey] = new UserAccessSnapshot("test", "Teacher",
            new HashSet<string> { TeacherAccessScopes.Class }, ["5H2"], []);
        var restricted = new ErpMealReportsController(db, new UserAccessService(db, new HttpContextAccessor { HttpContext = http }));
        var scoped = Assert.IsType<ErpMealReportPageDto>(Assert.IsType<OkObjectResult>((await restricted.Summary("coffee-frequency", "student-day")).Result).Value);
        Assert.Empty(scoped.Items);
    }

    [MySqlFact]
    public async Task CoffeeDefinitionMigrationInvalidatesOnlyBuiltMeasures()
    {
        await using var test = await TestDatabase.CreateAsync(targetMigration: "20261028030256_AddLeadershipActivityReportingDate");
        await using var db = test.Open();
        db.ErpMealReportBuilds.Add(new() { CompletedAtUtc = DateTime.UtcNow });
        db.ErpCanteenPurchases.Add(new()
        {
            ErpStudentId = "unmatched", PurchaseDate = new DateTime(2026, 10, 1), Quantity = 3,
            SourceRecordKey = "preserved-source", MatchStatus = "unmatched"
        });
        await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.False(await db.ErpMealReportBuilds.AnyAsync());
        Assert.Equal(1, await db.ErpCanteenPurchases.CountAsync());
        await new ErpMealReportAggregateService(db).RebuildAsync();
        Assert.Equal(3, await db.ErpMealDailyAggregates.SumAsync(x => x.CoffeeUnitCount));
        Assert.True(await db.ErpMealReportBuilds.AnyAsync());
    }
}
