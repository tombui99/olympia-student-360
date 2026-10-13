using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Converts mutable imported records into narrow daily report measures. No report request invokes
/// this builder; HTTP readers only scan these persisted measures.
/// </summary>
public sealed class ImportedReportAggregateService(AppDbContext db, CanvasReportingPeriodService canvasPeriods)
{
    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var latestChange = await db.ImportedReportChanges.AsNoTracking()
            .MaxAsync(item => (int?)item.Id, cancellationToken) ?? 0;
        var rows = new Dictionary<AggregateKey, ImportedReportAggregate>();
        var settingsRow = await db.StudentClassificationSettings.AsNoTracking()
            .OrderBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        var settings = settingsRow is null ? new StudentClassificationSettingsDto() : new StudentClassificationSettingsDto
        {
            RiskWindowDays = settingsRow.RiskWindowDays,
            HighRiskBehaviourCount = settingsRow.HighRiskBehaviourCount,
            HighRiskBusViolationCount = settingsRow.HighRiskBusViolationCount,
            HighRiskCanvasAverageBelow = settingsRow.HighRiskCanvasAverageBelow,
            CommendationWindowDays = settingsRow.CommendationWindowDays,
            CommendationPositiveBehaviourCount = settingsRow.CommendationPositiveBehaviourCount,
            CommendationCanvasAverageAtLeast = settingsRow.CommendationCanvasAverageAtLeast,
            CommendationMaxBusViolations = settingsRow.CommendationMaxBusViolations
        };
        var asOf = DateTime.UtcNow;
        var riskSince = asOf.AddDays(-settings.RiskWindowDays);
        var commendationSince = asOf.AddDays(-settings.CommendationWindowDays);
        var riskConcerns = new Dictionary<int, int>();
        var positiveRecent = new Dictionary<int, int>();
        var riskBus = new Dictionary<int, int>();
        var commendationBus = new Dictionary<int, int>();

        await foreach (var item in db.StudentBehaviours.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var type = StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType).Trim();
            var category = StudentClassificationService.BehaviourCategory(item.BehaviourType, item.ClassificationCategory);
            if (item.StudentProfileId is int behaviourStudentId)
            {
                if (category == "Concerning" && item.IncidentDate >= riskSince)
                    Increment(riskConcerns, behaviourStudentId);
                if (category == "Positive" && item.IncidentDate >= commendationSince)
                    Increment(positiveRecent, behaviourStudentId);
            }
            Add(rows, Base("student-behaviours", item.Student, item.IncidentDate,
                item.AcademicYear, item.Term, item.Program, item.Subject, item.MeasurementCycle,
                item.SourceStudentName) with
            {
                Type = type,
                Category = category,
                EnteredBy = Clean(item.ReporterName) ?? Clean(item.RecordedByEmail) ?? Clean(item.ImportHistory?.ImportedByName),
                AttentionCount = category == "Concerning" ? 1 : 0,
                Recent30Count = item.IncidentDate >= asOf.AddDays(-30) ? 1 : 0
            });
        }

        await foreach (var item in db.StudentProjects.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var active = !StudentDataEntryOptions.IsCompletedProjectStatus(item.Status);
            Add(rows, Base("student-projects", item.Student, item.ActivityDate ?? item.CreatedAt,
                item.AcademicYear, item.Term, item.Program, item.Subject, item.MeasurementCycle) with
            {
                Status = Clean(item.Status),
                EnteredBy = Clean(item.ImportHistory?.ImportedByName),
                AttentionCount = active ? 1 : 0
            });
        }

        await foreach (var item in db.StudentMentalHealthRecords.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            Add(rows, Base("student-mental-health", item.Student, item.RecordDate,
                item.AcademicYear, null, null, null, null) with
            {
                Type = Clean(item.ConcernType),
                Status = Clean(item.RiskLevel),
                EnteredBy = Clean(item.ImportHistory?.ImportedByName) ?? Clean(item.RecordedByName),
                AttentionCount = item.RiskLevel is "High" or "Medium" ? 1 : 0
            });
        }

        await foreach (var item in db.StudentBusAttendanceRecords.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            if (item.AttendanceDate >= riskSince) Increment(riskBus, item.StudentProfileId);
            if (item.AttendanceDate >= commendationSince) Increment(commendationBus, item.StudentProfileId);
            Add(rows, Base("student-bus-attendance", item.Student, item.AttendanceDate,
                item.AcademicYear, item.Term, null, null, null) with
            {
                Type = Clean(item.ViolationType),
                Status = Clean(item.TripDirection),
                EnteredBy = Clean(item.ImportHistory?.ImportedByName) ?? Clean(item.RecordedByName),
                AttentionCount = 1
            });
        }

        await foreach (var item in db.StudentHealthVisits.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory)
            .AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            var row = Base("student-health-visits", item.Student, item.ExamTime,
                null, null, null, null, null, item.FullName);
            row.SISUserId = Clean(item.Student?.SISUserId) ?? Clean(item.SISUserId);
            row.Homeroom = Clean(item.Student?.Homeroom) ?? Clean(item.ClassName);
            row.ClassName = Clean(item.ClassName);
            row.StudentKey = item.StudentProfileId.HasValue
                ? $"profile:{item.StudentProfileId.Value}"
                : row.SISUserId is not null
                    ? $"sis:{row.SISUserId.ToUpperInvariant()}"
                    : $"name:{row.StudentName.ToUpperInvariant()}:{row.Homeroom?.ToUpperInvariant()}";
            Add(rows, row with
            {
                Diagnosis = Clean(item.Diagnosis) ?? Clean(item.Classification) ?? Clean(item.Symptoms) ?? "—",
                Symptoms = Clean(item.Symptoms),
                Category = Clean(item.Classification),
                EnteredBy = Clean(item.ImportHistory?.ImportedByName) ?? Clean(item.ExaminedBy)
            });
        }

        var riskSnapshots = await BuildRiskSnapshots(rows.Values.ToList(), settings, asOf,
            riskConcerns, positiveRecent, riskBus, commendationBus, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.ImportedReportAggregates.ExecuteDeleteAsync(cancellationToken);
        await db.StudentRiskSnapshots.ExecuteDeleteAsync(cancellationToken);
        foreach (var batch in rows.Values.Chunk(1000))
        {
            db.ImportedReportAggregates.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        foreach (var batch in riskSnapshots.Chunk(1000))
        {
            db.StudentRiskSnapshots.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        await db.ImportedReportBuilds.ExecuteDeleteAsync(cancellationToken);
        db.ImportedReportBuilds.Add(new ImportedReportBuild
        {
            LastChangeId = latestChange,
            CompletedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        // Retain the highest marker so an empty-table AUTO_INCREMENT reset cannot make a
        // later source change appear older than LastChangeId after a database restart.
        await db.ImportedReportChanges.Where(item => item.Id < latestChange)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<List<StudentRiskSnapshot>> BuildRiskSnapshots(
        List<ImportedReportAggregate> aggregates, StudentClassificationSettingsDto settings,
        DateTime asOf, Dictionary<int, int> riskConcerns, Dictionary<int, int> positiveRecent,
        Dictionary<int, int> riskBus, Dictionary<int, int> commendationBus,
        CancellationToken cancellationToken)
    {
        var students = await db.StudentProfiles.AsNoTracking().Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var countsByStudent = aggregates.Where(item => item.StudentProfileId.HasValue)
            .GroupBy(item => item.StudentProfileId!.Value)
            .ToDictionary(group => group.Key, group => new
            {
                Behaviours = group.Where(item => item.Source == "student-behaviours")
                    .Sum(item => item.RecordCount),
                Positive = group.Where(item => item.Source == "student-behaviours" && item.Category == "Positive")
                    .Sum(item => item.RecordCount),
                Concerning = group.Where(item => item.Source == "student-behaviours" && item.Category == "Concerning")
                    .Sum(item => item.RecordCount),
                Projects = group.Where(item => item.Source == "student-projects")
                    .Sum(item => item.RecordCount),
                MentalHealth = group.Where(item => item.Source == "student-mental-health")
                    .Sum(item => item.RecordCount)
            });
        var sourceYears = await db.AcademicPeriods.AsNoTracking()
            .Select(item => item.AcademicYear).Distinct().ToListAsync(cancellationToken);
        sourceYears.AddRange(aggregates.Where(item => item.AcademicYear != null)
            .Select(item => item.AcademicYear!).Distinct());
        sourceYears.AddRange(await db.CanvasCourseScores.AsNoTracking()
            .Where(item => item.Enrollment!.Course!.EnrollmentTermName != null)
            .Select(item => item.Enrollment!.Course!.EnrollmentTermName!)
            .Distinct().ToListAsync(cancellationToken));
        sourceYears.Add(DashboardPeriodNormalizer.AcademicYear(asOf));
        var years = sourceYears.Select(DashboardPeriodNormalizer.AcademicYear)
            .Where(DashboardPeriodNormalizer.IsAcademicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<StudentRiskSnapshot>();
        foreach (var year in years)
        foreach (var term in new[] { "all", "hk1", "hk2" })
        {
            var period = await canvasPeriods.ResolveAsync(year, term, asOf);
            var scoreQuery = CanvasReportingPeriodService.FilterUsableScores(
                CanvasReportingPeriodService.FilterScores(
                    db.CanvasCourseScores.AsNoTracking().Where(item => item.StudentProfileId != null), period));
            var scores = await scoreQuery.GroupBy(item => item.StudentProfileId!.Value)
                .Select(group => new { StudentId = group.Key,
                    Average = group.Average(item => (decimal)item.FinalScore!.Value) })
                .ToDictionaryAsync(item => item.StudentId,
                    item => Math.Round(item.Average, 1), cancellationToken);

            foreach (var studentId in students)
            {
                countsByStudent.TryGetValue(studentId, out var counts);
                decimal? average = scores.TryGetValue(studentId, out var score) ? score : null;
                var concernCount = riskConcerns.GetValueOrDefault(studentId);
                var busCount = riskBus.GetValueOrDefault(studentId);
                var withBus = StudentClassificationService.ClassifyRisk(new StudentSignalSummary(
                    concernCount, positiveRecent.GetValueOrDefault(studentId), average, busCount,
                    commendationBus.GetValueOrDefault(studentId)), settings);
                var withoutBus = StudentClassificationService.ClassifyRisk(new StudentSignalSummary(
                    concernCount, positiveRecent.GetValueOrDefault(studentId), average, 0, 0), settings);
                result.Add(new StudentRiskSnapshot
                {
                    StudentProfileId = studentId, AcademicYear = year, Term = term, AsOfUtc = asOf,
                    AverageCanvasScore = average,
                    BehaviourCount = counts?.Behaviours ?? 0,
                    PositiveBehaviourCount = counts?.Positive ?? 0,
                    ConcerningBehaviourCount = counts?.Concerning ?? 0,
                    ProjectCount = counts?.Projects ?? 0,
                    MentalHealthCount = counts?.MentalHealth ?? 0,
                    RiskConcerningCount = concernCount, RiskBusCount = busCount,
                    RiskLevelWithBus = withBus.Level, RiskLevelWithoutBus = withoutBus.Level,
                    RiskFlagsWithBusJson = JsonSerializer.Serialize(withBus.Flags),
                    RiskFlagsWithoutBusJson = JsonSerializer.Serialize(withoutBus.Flags),
                    RiskFlagCountWithBus = withBus.Flags.Count,
                    RiskFlagCountWithoutBus = withoutBus.Flags.Count
                });
            }
        }
        return result;
    }

    private static void Increment(Dictionary<int, int> values, int id) =>
        values[id] = values.GetValueOrDefault(id) + 1;

    private static AggregateRow Base(string source, StudentProfile? student, DateTime date,
        string? year, string? term, string? program, string? subject, string? cycle,
        string? sourceName = null)
    {
        var name = Clean(student?.FullName) ?? Clean(sourceName) ?? "—";
        var grade = Clean(student?.GradeLevel) ?? Clean(student?.CanvasGradeLevel);
        return new AggregateRow
        {
            Source = source,
            StudentProfileId = student?.Id,
            StudentKey = student is not null ? $"id:{student.Id}" : $"source:{name.ToUpperInvariant()}:{grade?.ToUpperInvariant()}",
            StudentName = name,
            SISUserId = Clean(student?.SISUserId),
            Grade = grade,
            Homeroom = Clean(student?.Homeroom),
            CanvasGrade = Clean(student?.CanvasGradeLevel),
            Campus = Clean(student?.Campus),
            EducationLevel = Clean(student?.EducationLevel),
            Program = Clean(program) ?? Clean(student?.Program),
            Subject = Clean(subject),
            MeasurementCycle = Clean(cycle),
            AcademicYear = Clean(year),
            Term = Clean(term),
            EventDay = date.Date,
            LatestEvent = date,
            RecordCount = 1
        };
    }

    private static void Add(IDictionary<AggregateKey, ImportedReportAggregate> rows, AggregateRow row)
    {
        var key = new AggregateKey(row.Source, row.StudentProfileId, row.StudentKey, row.StudentName,
            row.SISUserId, row.Grade, row.Homeroom, row.CanvasGrade, row.ClassName, row.Campus, row.EducationLevel,
            row.Program, row.Subject, row.MeasurementCycle, row.AcademicYear, row.Term, row.EventDay,
            row.Type, row.Category, row.Status, row.Diagnosis, row.Symptoms, row.EnteredBy);
        if (rows.TryGetValue(key, out var existing))
        {
            existing.RecordCount += row.RecordCount;
            existing.AttentionCount += row.AttentionCount;
            existing.Recent30Count += row.Recent30Count;
            if (row.LatestEvent > existing.LatestEvent) existing.LatestEvent = row.LatestEvent;
            return;
        }
        rows.Add(key, new ImportedReportAggregate
        {
            Source = row.Source,
            StudentProfileId = row.StudentProfileId,
            StudentKey = row.StudentKey,
            StudentName = row.StudentName,
            SISUserId = row.SISUserId,
            Grade = row.Grade,
            Homeroom = row.Homeroom,
            CanvasGrade = row.CanvasGrade,
            ClassName = row.ClassName,
            Campus = row.Campus,
            EducationLevel = row.EducationLevel,
            Program = row.Program,
            Subject = row.Subject,
            MeasurementCycle = row.MeasurementCycle,
            AcademicYear = row.AcademicYear,
            Term = row.Term,
            EventDay = row.EventDay,
            LatestEvent = row.LatestEvent,
            Type = row.Type,
            Category = row.Category,
            Status = row.Status,
            Diagnosis = row.Diagnosis,
            Symptoms = row.Symptoms,
            EnteredBy = row.EnteredBy,
            RecordCount = row.RecordCount,
            AttentionCount = row.AttentionCount,
            Recent30Count = row.Recent30Count
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AggregateKey(string Source, int? StudentProfileId, string StudentKey,
        string StudentName, string? SISUserId, string? Grade, string? Homeroom, string? CanvasGrade,
        string? ClassName,
        string? Campus, string? EducationLevel, string? Program, string? Subject,
        string? MeasurementCycle, string? AcademicYear, string? Term, DateTime EventDay,
        string? Type, string? Category, string? Status, string? Diagnosis, string? Symptoms,
        string? EnteredBy);

    private sealed record AggregateRow
    {
        public string Source { get; init; } = default!;
        public int? StudentProfileId { get; init; }
        public string StudentKey { get; set; } = default!;
        public string StudentName { get; init; } = default!;
        public string? SISUserId { get; set; }
        public string? Grade { get; init; }
        public string? Homeroom { get; set; }
        public string? CanvasGrade { get; init; }
        public string? ClassName { get; set; }
        public string? Campus { get; init; }
        public string? EducationLevel { get; init; }
        public string? Program { get; init; }
        public string? Subject { get; init; }
        public string? MeasurementCycle { get; init; }
        public string? AcademicYear { get; init; }
        public string? Term { get; init; }
        public DateTime EventDay { get; init; }
        public DateTime LatestEvent { get; init; }
        public string? Type { get; init; }
        public string? Category { get; init; }
        public string? Status { get; init; }
        public string? Diagnosis { get; init; }
        public string? Symptoms { get; init; }
        public string? EnteredBy { get; init; }
        public int RecordCount { get; init; }
        public int AttentionCount { get; init; }
        public int Recent30Count { get; init; }
    }
}
