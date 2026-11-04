using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.SisSync;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [MySqlTheory]
    [InlineData("olympia_imported_report_aggregates")]
    [InlineData("olympia_erp_meal_report_aggregates")]
    public async Task SisLeaseWaitsForReportPublishersAndReleasesPartialAcquisition(string reportLock)
    {
        await using var test = await TestDatabase.CreateAsync(createSchema: false);
        await using var publisher = test.Open();
        await using var importer = test.Open();
        await publisher.Database.OpenConnectionAsync();
        await LockScalar(publisher, "SELECT GET_LOCK(@name, 0)", reportLock);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => SisProjectionLease.AcquireAsync(
                importer, default, TimeSpan.FromMilliseconds(200)));
            Assert.Equal(System.Data.ConnectionState.Closed, importer.Database.GetDbConnection().State);
            await importer.Database.OpenConnectionAsync();
            foreach (var name in new[] { "olympia_canvas_warehouse", "olympia_imported_report_aggregates", "olympia_erp_meal_report_aggregates" })
            {
                if (name == reportLock) continue;
                Assert.Equal(1, await LockScalar(importer, "SELECT GET_LOCK(@name, 0)", name));
                await LockScalar(importer, "SELECT RELEASE_LOCK(@name)", name);
            }
            await importer.Database.CloseConnectionAsync();
        }
        finally
        {
            await LockScalar(publisher, "SELECT RELEASE_LOCK(@name)", reportLock);
            await publisher.Database.CloseConnectionAsync();
        }
        await using (await SisProjectionLease.AcquireAsync(importer, default, TimeSpan.FromSeconds(2))) { }
        await AssertWarehouseLockReleased(importer);
    }

    [MySqlFact]
    public async Task SisStudentReplayBatchesUpdatesAndPreservesManualAndCanvasFields()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var counter = new SisCommandCounter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(counter).Options);
        var students = Enumerable.Range(1, 500).Select(index =>
        {
            var row = JsonSerializer.SerializeToNode(SisFixtures.Student())!;
            row["id"] = "record-" + index;
            row["student_user_id"] = "user-" + index;
            row["student_code"] = "code-" + index;
            row["username"] = "student-" + index;
            row["email"] = "student-" + index + "@school.test";
            return (object)row;
        }).ToArray();
        using (var http = SisFixtures.Http(students))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        db.ChangeTracker.Clear();
        var profile = await db.StudentProfiles.SingleAsync(x => x.SchoolOnlineUserId == "user-1");
        profile.Campus = "Manual campus";
        profile.Program = "Manual programme";
        profile.CanvasUserId = 456;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        counter.Count = 0;
        using (var http = SisFixtures.Http(students))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        Assert.True(counter.Count < 80, $"Expected bounded student page commands; observed {counter.Count}.");
        db.ChangeTracker.Clear();
        profile = await db.StudentProfiles.SingleAsync(x => x.SchoolOnlineUserId == "user-1");
        Assert.Equal("Manual campus", profile.Campus);
        Assert.Equal("Manual programme", profile.Program);
        Assert.Equal(456, profile.CanvasUserId);
        Assert.Equal(500, await db.StudentProfiles.CountAsync());
        Assert.Equal(500, await db.SISStudentParents.CountAsync());
    }

    [MySqlFact]
    public async Task SisHealthPageUsesBoundedDatabaseCommandsAndPreservesParameterizedTextAndIdentity()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var counter = new SisCommandCounter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(counter).Options);
        const string literal = "O'Hara – Tiếng Việt; '); DELETE FROM StudentProfiles; --";
        var visits = Enumerable.Range(1, 500).Select(index =>
        {
            var row = JsonSerializer.SerializeToNode(SisFixtures.HealthVisit(notes: literal))!;
            row["id"] = "visit-" + index;
            return (object)row;
        }).ToArray();
        using (var http = SisFixtures.Http([SisFixtures.Student()], healthVisits: visits))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(health: true), true, default);
        db.ChangeTracker.Clear();
        var ids = await db.StudentHealthVisits.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        Assert.Equal(500, ids.Length);
        Assert.Equal(500, await db.StudentHealthVisits.CountAsync(x => x.Notes == literal && x.StudentProfileId != null));
        counter.Count = 0;
        using (var http = SisFixtures.Http([SisFixtures.Student()], healthVisits: visits))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(health: true), true, default);
        Assert.True(counter.Count < 80, $"Expected bounded page commands; observed {counter.Count}.");
        db.ChangeTracker.Clear();
        Assert.Equal(ids, await db.StudentHealthVisits.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Single(await db.StudentProfiles.ToListAsync());
        Assert.Single(await db.SISStudentParents.ToListAsync());
    }

    [MySqlFact]
    public async Task SisReportRefreshUsesBulkPublicationAndCanRecoverWithoutReimportingCommittedData()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        var counter = new SisCommandCounter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options).AddInterceptors(counter).Options);
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        var student = await db.StudentProfiles.SingleAsync();
        var visits = Enumerable.Range(0, 2100).Select(index => new StudentHealthVisit
        {
            StudentProfileId = student.Id, SisSourceKey = "fixture", SisVisitId = "report-visit-" + index,
            FullName = "Test student", Diagnosis = "Diagnosis", ExamTime = new DateTime(2026, 9, 1).AddDays(index)
        });
        await MySqlRowBatchWriter.InsertAsync(db, visits, default);
        counter.Count = 0;
        await new SisReportRefresher().RunAsync(db, default);
        Assert.True(counter.Count < 100, $"Expected bulk report publication; observed {counter.Count} commands.");
        Assert.Equal(2100, await db.ImportedReportAggregates.Where(x => x.Source == "student-health-visits").SumAsync(x => x.RecordCount));
        var publishedMarker = (await db.ImportedReportBuilds.AsNoTracking().SingleAsync()).LastChangeId;
        var watermark = (await db.SisSyncStates.AsNoTracking().SingleAsync(x => x.Endpoint == "students")).LastSuccessfulWatermark;
        db.StudentBehaviours.Add(new StudentBehaviour { StudentProfileId = student.Id,
            IncidentDate = new DateTime(2026, 11, 3), BehaviourType = "Test behaviour" });
        await db.SaveChangesAsync();
        counter.FailBuildPublication = true;
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => new SisReportRefresher().RunAsync(db, default));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal(publishedMarker, (await db.ImportedReportBuilds.AsNoTracking().SingleAsync()).LastChangeId);
        Assert.False(await db.ImportedReportAggregates.AnyAsync(x => x.Source == "student-behaviours"));
        Assert.Equal("completed", (await db.SisSyncRuns.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(watermark, (await db.SisSyncStates.AsNoTracking().SingleAsync(x => x.Endpoint == "students")).LastSuccessfulWatermark);
        counter.FailBuildPublication = false;
        await new SisReportRefresher().RunAsync(db, default);
        Assert.Equal(1, await db.ImportedReportAggregates.Where(x => x.Source == "student-behaviours").SumAsync(x => x.RecordCount));
        Assert.Equal(await db.ImportedReportChanges.MaxAsync(x => x.Id), (await db.ImportedReportBuilds.AsNoTracking().SingleAsync()).LastChangeId);
        Assert.Single(await db.SisSyncRuns.ToListAsync());
    }

    [MySqlFact]
    public async Task SisSafeDiagnosticsIncludeDatabaseCodeWithoutSqlOrCredentials()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        var cause = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("SELECT * FROM NoSuchSisDiagnosticTable"));
        var safe = SisSyncRunner.SafeError(new InvalidOperationException("private-password; SELECT sensitive_data", cause));
        Assert.Contains("MySQL error 1146", safe);
        Assert.DoesNotContain("private-password", safe);
        Assert.DoesNotContain("NoSuchSisDiagnosticTable", safe);
        Assert.DoesNotContain("sensitive_data", safe);
    }

    private static async Task<int> LockScalar(AppDbContext db, string sql, string name)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class SisCommandCounter : DbCommandInterceptor
    {
        public int Count;
        public bool FailBuildPublication;
        private void Check(DbCommand command)
        {
            Interlocked.Increment(ref Count);
            if (FailBuildPublication && command.CommandText.Contains("INSERT INTO `ImportedReportBuilds`", StringComparison.Ordinal))
                throw new InvalidOperationException("Injected report publication failure");
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Check(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Check(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Check(command); return ValueTask.FromResult(result); }
    }
}
