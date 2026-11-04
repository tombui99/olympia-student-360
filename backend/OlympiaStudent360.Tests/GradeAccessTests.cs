using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [Theory]
    [InlineData("1", "Primary")]
    [InlineData("4", "Primary")]
    [InlineData("5", "LowerSecondary")]
    [InlineData("8", "LowerSecondary")]
    [InlineData("9", "UpperSecondary")]
    [InlineData("12", "UpperSecondary")]
    public void SchoolLevelGradesHaveTheSchoolRequestedBoundaries(string grade, string expected)
    {
        Assert.Equal(expected, Assert.Single(TeacherSchoolLevels.Values, level =>
            TeacherSchoolLevels.IncludesGrade(level, grade)));
    }

    private static UserAccessSnapshot GradeAccess(params string[] grades) =>
        new("grade-teacher", "Teacher", new HashSet<string> { TeacherAccessScopes.Grade }, [], [])
        { GradeLevels = grades };

    [MySqlFact]
    public async Task SisGradeNineIsProjectedAsUpperSecondary()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var source = System.Text.Json.JsonSerializer.SerializeToNode(SisFixtures.Student())!;
        source["grade"]!["code"] = "09";
        using var document = System.Text.Json.JsonDocument.Parse(source.ToJsonString());
        await new OlympiaStudent360.Api.SisSync.SisStudentProjector(db).ProjectAsync(document.RootElement, default);
        await db.SaveChangesAsync();
        var student = await db.StudentProfiles.SingleAsync();
        Assert.Equal("9", student.GradeLevel);
        Assert.Equal("UpperSecondary", student.EducationLevel);
    }

    [MySqlFact]
    public async Task UnrecognizedPersistedScopesDoNotGrantAllStudentAccess()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var teacher = new ApplicationUser { UserName = "legacy-teacher", FullName = "Teacher" };
        db.Users.Add(teacher);
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        var service = new UserAccessService(db, new HttpContextAccessor { HttpContext = http });
        Assert.True((await service.LoadAsync(teacher.Id, "Teacher")).HasAll); // Legacy accounts without grants.
        db.TeacherAccessGrants.Add(new TeacherAccessGrant { UserId = teacher.Id, Scope = "UnknownScope" });
        db.StudentProfiles.Add(new StudentProfile { SISUserId = "student", FullName = "Student", GradeLevel = "8" });
        await db.SaveChangesAsync();
        var access = await service.LoadAsync(teacher.Id, "Teacher");
        http.Items[UserAccessService.HttpContextItemKey] = access;
        Assert.False(access.HasAll);
        Assert.False(access.CanAccessAcademic);
        Assert.False(access.CanAccessStudent("8A", "8", null));
        Assert.Empty(await service.ApplyStudentScope(db.StudentProfiles).ToListAsync());
    }

    [MySqlFact]
    public async Task GradeScopesUseOfficialGradesAndMatchDetailAndSqlFilters()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var students = new[]
        {
            new StudentProfile { SISUserId = "eight-a", FullName = "Eight A", GradeLevel = "8", Homeroom = "8A" },
            new StudentProfile { SISUserId = "eight-b", FullName = "Eight B", GradeLevel = "8", Homeroom = "8B", CanvasGradeLevel = "9" },
            new StudentProfile { SISUserId = "nine", FullName = "Nine", GradeLevel = "9", Homeroom = "9A", CanvasGradeLevel = "8" },
            new StudentProfile { SISUserId = "fallback", FullName = "Fallback", CanvasGradeLevel = "8" },
            new StudentProfile { SISUserId = "blank", FullName = "Blank SIS", GradeLevel = "  ", CanvasGradeLevel = " 8 " },
            new StudentProfile { SISUserId = "unknown", FullName = "Unknown", Homeroom = "8C" },
            new StudentProfile { SISUserId = "four", FullName = "Four", GradeLevel = "4", Homeroom = "4A" }
        };
        db.StudentProfiles.AddRange(students);
        await db.SaveChangesAsync();
        var http = new DefaultHttpContext();
        var service = new UserAccessService(db, new HttpContextAccessor { HttpContext = http });
        foreach (var access in new[]
        {
            GradeAccess("8"), GradeAccess("9"), GradeAccess(),
            new UserAccessSnapshot("lower", "Teacher", new HashSet<string> { TeacherAccessScopes.SchoolLevel }, [], ["LowerSecondary"]),
            new UserAccessSnapshot("upper", "Teacher", new HashSet<string> { TeacherAccessScopes.SchoolLevel }, [], ["UpperSecondary"]),
            new UserAccessSnapshot("combined", "Teacher", new HashSet<string> { TeacherAccessScopes.Class, TeacherAccessScopes.Grade,
                TeacherAccessScopes.SchoolLevel }, ["9A"], ["Primary"]) { GradeLevels = ["8"] },
            new UserAccessSnapshot("all", "Teacher", new HashSet<string> { TeacherAccessScopes.All, TeacherAccessScopes.Grade }, [], [])
        })
        {
            http.Items[UserAccessService.HttpContextItemKey] = access;
            var expected = students.Where(student => access.CanAccessStudent(student.Homeroom, student.GradeLevel, student.CanvasGradeLevel))
                .Select(student => student.Id).Order().ToArray();
            Assert.Equal(expected, await service.ApplyStudentScope(db.StudentProfiles).OrderBy(student => student.Id)
                .Select(student => student.Id).ToArrayAsync());
        }
        var gradeEight = GradeAccess("8");
        Assert.True(gradeEight.CanAccessAcademic);
        Assert.False(gradeEight.CanAccessHealth);
        Assert.False(gradeEight.CanAccessBus);
        Assert.False(gradeEight.CanAccessStudent("9A", "9", "8"));
        Assert.NotEqual(gradeEight.CacheKey, GradeAccess("9").CacheKey);

        // Generic report filters must use the same student permissions. An unlinked record's
        // class label does not establish a grade assignment.
        db.StudentHealthVisits.AddRange(
            new() { SisVisitId = "eight", Student = students[0], FullName = "Eight A", ClassName = "8A", ExamTime = DateTime.UtcNow },
            new() { SisVisitId = "nine", Student = students[2], FullName = "Nine", ClassName = "9A", ExamTime = DateTime.UtcNow },
            new() { SisVisitId = "unlinked", FullName = "Unlinked", ClassName = "8A", ExamTime = DateTime.UtcNow });
        await db.SaveChangesAsync();
        http.Items[UserAccessService.HttpContextItemKey] = gradeEight;
        Assert.Single(await service.ApplyStudentScope(db.StudentHealthVisits, visit => visit.Student).ToListAsync());
        Assert.Single(await service.ApplyStudentOrHomeroomScope(db.StudentHealthVisits,
            visit => visit.Student, visit => visit.ClassName).ToListAsync());
    }

    [MySqlFact]
    public async Task BothAccessEditorsPersistGradeGrantsAndRejectInvalidSelectionsBeforeWriting()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var services = new ServiceCollection().AddLogging().AddSingleton(db);
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
        await using var provider = services.BuildServiceProvider();
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Teacher"))).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole("SchoolAdministration"))).Succeeded);
        var admin = new AdminUsersController(users, roles, db)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var createdResult = await admin.CreateUser(new CreateAdminUserDto
        {
            Username = "grade.teacher", FullName = "Grade teacher", Password = "Test-grade-123!", Role = "Teacher",
            AccessScopes = ["Grade"], GradeLevels = ["9", "8", "8"]
        });
        var created = Assert.IsType<AdminUserDto>(Assert.IsType<ObjectResult>(createdResult.Result).Value);
        Assert.Equal(new[] { "8", "9" }, created.GradeLevels);
        var editor = new UserAccessController(db, users);
        var settings = Assert.IsType<UserAccessManagementDto>(Assert.IsType<OkObjectResult>((await editor.Get()).Result).Value);
        Assert.Contains("Grade", settings.AvailableScopes);
        Assert.Equal(TeacherGradeLevels.Values, settings.AvailableGradeLevels);
        Assert.Equal(created.GradeLevels, Assert.Single(settings.Users).GradeLevels);
        var accessService = new UserAccessService(db, new HttpContextAccessor());
        var originalKey = (await accessService.LoadAsync(created.Id, "Teacher")).CacheKey;
        var updatedResult = await editor.Update(created.Id, new UpdateTeacherAccessDto { Scopes = ["Grade"], GradeLevels = [" 8 "] });
        var updated = Assert.IsType<TeacherAccessUserDto>(Assert.IsType<OkObjectResult>(updatedResult.Result).Value);
        Assert.Equal(new[] { "8" }, updated.GradeLevels);
        var loaded = await accessService.LoadAsync(created.Id, "Teacher");
        Assert.NotEqual(originalKey, loaded.CacheKey);
        Assert.False(loaded.CanAccessStudent("9A", "9", null));
        foreach (var grades in new[] { Array.Empty<string>(), ["0"], ["13"], ["8", "invalid"] })
        {
            Assert.IsType<BadRequestObjectResult>((await editor.Update(created.Id,
                new UpdateTeacherAccessDto { Scopes = ["Grade"], GradeLevels = grades.ToList() })).Result);
            Assert.IsType<BadRequestObjectResult>((await admin.UpdateUser(created.Id,
                new UpdateAdminUserDto { FullName = "Grade teacher", Role = "Teacher", AccessScopes = ["Grade"], GradeLevels = grades.ToList() })).Result);
            Assert.Equal(new[] { "8" }, (await accessService.LoadAsync(created.Id, "Teacher")).GradeLevels);
        }
        Assert.IsType<BadRequestObjectResult>((await admin.UpdateUser(created.Id, new UpdateAdminUserDto
        { FullName = "Grade teacher", Role = "Teacher", AccessScopes = ["UnknownScope"] })).Result);
        var listed = Assert.IsType<AdminUserPageDto>(Assert.IsType<OkObjectResult>((await admin.GetUsers()).Result).Value);
        Assert.Equal(new[] { "8" }, Assert.Single(listed.Items).GradeLevels);
        await editor.Update(created.Id, new UpdateTeacherAccessDto { Scopes = ["SchoolLevel"], SchoolLevels = ["UpperSecondary"] });
        Assert.Empty(await db.TeacherGradeAccesses.ToListAsync());
        loaded = await accessService.LoadAsync(created.Id, "Teacher");
        Assert.True(loaded.CanAccessStudent("9A", "9", null));
        Assert.False(loaded.CanAccessStudent("8A", "8", null));
        await admin.UpdateUser(created.Id, new UpdateAdminUserDto
        { FullName = "Administrator", Role = "SchoolAdministration" });
        Assert.Empty(await db.TeacherGradeAccesses.ToListAsync());
        Assert.Empty(await db.TeacherSchoolLevelAccesses.ToListAsync());
    }

    [MySqlFact]
    public async Task GradeMigrationResumesPartialDdlAndCorrectsOnlyGradeNine()
    {
        await using var test = await TestDatabase.CreateAsync(targetMigration: "20261104145633_ScopeReportInvalidation");
        await using var db = test.Open();
        foreach (var grade in new[] { "4", "8", "9", "10", "12" })
            db.StudentProfiles.Add(new StudentProfile { SISUserId = "grade-" + grade, FullName = "Student " + grade,
                GradeLevel = grade, EducationLevel = grade is "8" or "9" ? "LowerSecondary" : grade == "4" ? "Primary" : "UpperSecondary",
                Campus = "Manual campus", Program = "Manual programme" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ImportedReportChanges");
        // MySQL DDL commits separately; simulate interruption before the index/history row.
        var migration = new OlympiaStudent360.Api.Migrations.AddTeacherGradeAccessAndCorrectSchoolLevels();
        var generator = db.GetService<IMigrationsSqlGenerator>();
        var create = migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateTableOperation>().Single();
        foreach (var command in generator.Generate([create], db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.MigrateWithRecoveryAsync();
        db.ChangeTracker.Clear();
        var nine = await db.StudentProfiles.SingleAsync(student => student.GradeLevel == "9");
        Assert.Equal("UpperSecondary", nine.EducationLevel);
        Assert.Equal("Manual campus", nine.Campus);
        Assert.Equal("Manual programme", nine.Program);
        Assert.Equal("LowerSecondary", (await db.StudentProfiles.SingleAsync(student => student.GradeLevel == "8")).EducationLevel);
        var change = Assert.Single(await db.ImportedReportChanges.ToListAsync());
        Assert.Equal(nine.Id, change.StudentProfileId);
        Assert.Equal(ReportArea.Imported, change.Areas);
        Assert.False(change.Areas.HasFlag(ReportArea.Risk));
        Assert.False(change.Areas.HasFlag(ReportArea.Meals));
        await db.MigrateWithRecoveryAsync();
        Assert.Single(await db.ImportedReportChanges.ToListAsync());
        var indexes = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TeacherGradeAccesses' AND INDEX_NAME = 'IX_TeacherGradeAccesses_UserId_GradeLevel'").SingleAsync();
        Assert.Equal(2, indexes);
    }
}
