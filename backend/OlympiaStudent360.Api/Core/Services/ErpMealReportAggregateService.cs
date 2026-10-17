using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Builds student/day/meal measures and drill-down rows outside the HTTP request path.</summary>
public sealed class ErpMealReportAggregateService(AppDbContext db)
{
    private sealed record StudentInfo(int Id, string FullName, string? Grade,
        string? CanvasGrade, string? Homeroom);
    private sealed record AggregateKey(string StudentKey, DateTime Day, string Meal);

    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var lastRunId = await db.ErpMealSyncRuns.AsNoTracking()
            .MaxAsync(x => (int?)x.Id, cancellationToken) ?? 0;
        var lastStudentChangeId = await db.ImportedReportChanges.AsNoTracking()
            .MaxAsync(x => (int?)x.Id, cancellationToken) ?? 0;
        var students = await db.StudentProfiles.AsNoTracking()
            .Select(x => new StudentInfo(x.Id, x.FullName, x.GradeLevel,
                x.CanvasGradeLevel, x.Homeroom))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var aggregates = new Dictionary<AggregateKey, ErpMealDailyAggregate>();
        var details = new List<ErpMealReportDetail>();
        var orderedSeen = new HashSet<AggregateKey>();
        var noOrderSeen = new HashSet<AggregateKey>();

        ErpMealDailyAggregate GetOrAdd(int? profileId, string erpStudentId,
            string? sourceName, string? sourceClass, DateTime day, string meal)
        {
            students.TryGetValue(profileId ?? -1, out var student);
            var studentKey = student is not null
                ? $"profile:{student.Id}" : $"erp:{erpStudentId.Trim().ToUpperInvariant()}";
            var date = day.Date;
            var key = new AggregateKey(studentKey, date, meal);
            if (aggregates.TryGetValue(key, out var existing)) return existing;
            var weekOffset = ((int)date.DayOfWeek + 6) % 7;
            var row = new ErpMealDailyAggregate
            {
                StudentKey = studentKey,
                StudentProfileId = student?.Id,
                ErpStudentId = erpStudentId.Trim(),
                StudentName = student?.FullName ?? sourceName?.Trim() ?? erpStudentId.Trim(),
                Grade = student?.Grade ?? student?.CanvasGrade,
                ClassName = student?.Homeroom ?? sourceClass?.Trim(),
                EventDay = date,
                WeekStart = date.AddDays(-weekOffset),
                MonthStart = new DateTime(date.Year, date.Month, 1),
                Meal = meal
            };
            aggregates.Add(key, row);
            return row;
        }

        static ErpMealReportDetail NewDetail(ErpMealDailyAggregate row, string report, int sourceRowId) => new()
        {
            Report = report,
            StudentKey = row.StudentKey,
            StudentProfileId = row.StudentProfileId,
            ErpStudentId = row.ErpStudentId,
            StudentName = row.StudentName,
            Grade = row.Grade,
            ClassName = row.ClassName,
            EventDay = row.EventDay,
            WeekStart = row.WeekStart,
            MonthStart = row.MonthStart,
            SourceRowId = sourceRowId
        };

        await foreach (var item in db.ErpMealStatuses.AsNoTracking()
            .Select(x => new { x.Id, x.StudentProfileId, x.ErpStudentId, x.StudentName,
                x.ClassCode, x.MealDate, x.Meal, x.Feed, x.OrderStatus, x.TicketStatus })
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var orderedNoTicket = item.Feed == ErpMealFeeds.MissingTicket &&
                Is(item.OrderStatus, "Có đặt ăn") && Is(item.TicketStatus, "Không lấy phiếu ăn");
            var noOrder = item.Feed == ErpMealFeeds.MissingOrder &&
                Is(item.OrderStatus, "Không đặt ăn");
            if (!orderedNoTicket && !noOrder) continue;
            var row = GetOrAdd(item.StudentProfileId, item.ErpStudentId, item.StudentName,
                item.ClassCode, item.MealDate, item.Meal.Trim());
            var key = new AggregateKey(row.StudentKey, row.EventDay, row.Meal);
            // Exports contain repeated rows for a student/day/meal. Count the meal once.
            if (orderedNoTicket && orderedSeen.Add(key))
            {
                row.OrderedNoTicketCount = 1;
                var detail = NewDetail(row, "ordered-no-ticket", item.Id);
                detail.Meal = row.Meal;
                detail.OrderStatus = item.OrderStatus;
                detail.TicketStatus = item.TicketStatus;
                details.Add(detail);
            }
            if (noOrder && noOrderSeen.Add(key))
            {
                row.NoOrderCount = 1;
                var detail = NewDetail(row, "no-order", item.Id);
                detail.Meal = row.Meal;
                detail.OrderStatus = item.OrderStatus;
                detail.TicketStatus = item.TicketStatus;
                details.Add(detail);
            }
        }

        await foreach (var item in db.ErpCanteenPurchases.AsNoTracking()
            .Select(x => new { x.Id, x.StudentProfileId, x.ErpStudentId, x.StudentName,
                x.ClassCode, x.PurchaseDate, x.ProductCode, x.ProductName, x.Quantity })
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            // CFS_ and CF_ identify the Coffee Shop-coded items in this export.
            // The ERP has no formal sales-location or transaction/receipt field.
            if (item.ProductCode is null ||
                !(item.ProductCode.StartsWith("CFS_", StringComparison.OrdinalIgnoreCase) ||
                  item.ProductCode.StartsWith("CF_", StringComparison.OrdinalIgnoreCase))) continue;
            var row = GetOrAdd(item.StudentProfileId, item.ErpStudentId, item.StudentName,
                item.ClassCode, item.PurchaseDate, "");
            row.CoffeeLineCount++;
            row.CoffeeUnitCount += item.Quantity;
            var detail = NewDetail(row, "coffee-frequency", item.Id);
            detail.ProductCode = item.ProductCode;
            detail.ProductName = item.ProductName;
            detail.Quantity = item.Quantity;
            details.Add(detail);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ErpMealReportDetails.ExecuteDeleteAsync(cancellationToken);
        await db.ErpMealDailyAggregates.ExecuteDeleteAsync(cancellationToken);
        foreach (var batch in aggregates.Values.Chunk(1000))
        {
            db.ErpMealDailyAggregates.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        foreach (var batch in details.Chunk(1000))
        {
            db.ErpMealReportDetails.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        await db.ErpMealReportBuilds.ExecuteDeleteAsync(cancellationToken);
        db.ErpMealReportBuilds.Add(new ErpMealReportBuild
        {
            LastSyncRunId = lastRunId,
            LastStudentChangeId = lastStudentChangeId,
            CompletedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return aggregates.Count;
    }

    private static bool Is(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
