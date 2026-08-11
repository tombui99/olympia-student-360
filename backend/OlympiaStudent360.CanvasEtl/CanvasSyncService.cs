using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.CanvasEtl;

internal sealed partial class CanvasSyncService
{
    private readonly AppDbContext _db;
    private readonly CanvasApiClient _canvas;
    private readonly CanvasEtlOptions _options;

    public CanvasSyncService(
        AppDbContext db,
        CanvasApiClient canvas,
        CanvasEtlOptions options)
    {
        _db = db;
        _canvas = canvas;
        _options = options;
    }

    public async Task<CanvasSyncSummary> Run(bool replaceExisting, CancellationToken cancellationToken)
    {
        await _db.Database.MigrateAsync(cancellationToken);

        if (_options.SinceUtc is { } sinceUtc)
        {
            Console.WriteLine(
                $"Incremental Canvas submission window starts at {sinceUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}; " +
                "active-term enrollment grade totals will still be fully refreshed.");
        }

        var sourceStudents = await _canvas.GetStudents(cancellationToken);
        Console.WriteLine(
            $"Canvas returned {sourceStudents.Count} account student users for identity lookup only.");
        if (sourceStudents.Count == 0 && !_options.AllowEmptyStudents)
        {
            throw new InvalidOperationException(
                "Canvas returned no students. Verify Canvas__AccountId and that the token can read account users. " +
                "Set Canvas__AllowEmptyStudents=true only when an account with no students is intentional.");
        }

        var courses = await _canvas.GetCourses(cancellationToken);
        Console.WriteLine(
            $"Canvas returned {courses.Count} courses for the " +
            $"{(_options.IncludeAllCourses ? "unfiltered all-course" :
                _options.IncludeCompletedCourses ? "full academic-history" : "active daily")} scope.");
        if (courses.Count == 0 && !_options.AllowEmptyCourses)
        {
            throw new InvalidOperationException(
                "Canvas returned no courses. Verify Canvas__AccountId and the token's account permissions. " +
                "Set Canvas__AllowEmptyCourses=true only when an empty account is intentional.");
        }

        if (replaceExisting)
        {
            await ReplaceCanvasWarehouse(cancellationToken);
        }

        var studentCounts = await LinkSISStudents(sourceStudents, cancellationToken);
        Console.WriteLine(
            $"Canvas/SIS identity linking: {studentCounts.Matched} matched existing SIS profiles, " +
            $"{studentCounts.CanvasLinksCreated} Canvas user links created, " +
            $"{studentCounts.Unmatched} Canvas users unmatched, " +
            $"{studentCounts.MissingSISUserIds} Canvas users missing sis_user_id. " +
            "No StudentProfile rows were created or updated from Canvas-owned profile data.");

        var relinkedFacts = await RelinkExistingFacts(cancellationToken);
        if (relinkedFacts > 0)
        {
            Console.WriteLine($"Relinked {relinkedFacts} existing Canvas facts to SIS student profiles.");
        }

        var deletedCourseIds = courses
            .Where(course => string.Equals(course.WorkflowState, "deleted", StringComparison.OrdinalIgnoreCase))
            .Select(course => course.Id)
            .ToArray();
        if (deletedCourseIds.Length > 0)
        {
            var deletedRows = await _db.CanvasCourses
                .Where(course => deletedCourseIds.Contains(course.CanvasCourseId))
                .ExecuteDeleteAsync(cancellationToken);
            Console.WriteLine($"Removed {deletedRows} Canvas courses that Canvas now reports as deleted.");
        }

        courses = courses
            .Where(course => !string.Equals(course.WorkflowState, "deleted", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var studentsByCode = await _db.StudentProfiles
            .Where(student => student.SISUserId != "")
            .ToDictionaryAsync(student => student.SISUserId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var studentsByCanvasId = await _db.StudentProfiles
            .Where(student => student.CanvasUserId != null)
            .ToDictionaryAsync(student => student.CanvasUserId!.Value, cancellationToken);
        var sisUserIdByCanvasId = sourceStudents
            .Where(student => !string.IsNullOrWhiteSpace(student.SisUserId))
            .GroupBy(student => student.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Select(student => student.SisUserId!.Trim()).First());

        var totals = new SyncCounts();
        // Canvas throttles large bursts heavily. Keep full/reconciliation reads bounded and use an
        // even smaller active-course batch for daily runs. Submission payloads remain watermark
        // filtered in incremental mode; dimensions and enrollment grade totals are refreshed.
        var courseBatchSize = _options.IncludeCompletedCourses ? 8 : 4;
        foreach (var courseBatch in courses.Chunk(courseBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine(
                $"Fetching Canvas course batch starting at {courseBatch[0].Id} " +
                $"({courseBatch.Length} courses).");
            var courseData = await Task.WhenAll(courseBatch.Select(course =>
                FetchCourseData(course, cancellationToken)));
            foreach (var data in courseData)
            {
                Console.WriteLine($"Syncing Canvas course {data.Course.Id}: {data.Course.Name ?? "(unnamed)"}");
                var counts = await SaveCourse(
                    data.Course,
                    data.Assignments,
                    data.Groups,
                    data.Enrollments,
                    data.GradingPeriodEnrollments,
                    data.Submissions,
                    true,
                    _options.SinceUtc is null,
                    studentsByCode,
                    studentsByCanvasId,
                    sisUserIdByCanvasId,
                    cancellationToken);
                totals.Add(counts);
            }
        }

        Console.WriteLine(
            $"Canvas sync complete: {courses.Count} courses, {totals.Assignments} assignments, " +
            $"{totals.Enrollments} enrollments, {totals.Scores} course scores, {totals.Submissions} submissions. " +
            $"Unmatched SIS student references: {totals.UnmatchedStudents}; " +
            $"score rows without grades: {totals.ScoresWithoutGrades}; " +
            $"student match rate: {Percent(totals.MatchedStudents, totals.StudentUsers):0.0}%.");

        return new CanvasSyncSummary(
            sourceStudents.Count,
            studentCounts.Matched,
            studentCounts.CanvasLinksCreated,
            studentCounts.Unmatched,
            courses.Count,
            totals.Assignments,
            totals.Enrollments,
            totals.Scores,
            totals.Submissions,
            totals.UnmatchedStudents,
            totals.StudentUsers,
            totals.MatchedStudents);
    }

    private async Task<CanvasCourseData> FetchCourseData(
        CanvasCourseDto course,
        CancellationToken cancellationToken)
    {
        // Course-scoped reads are independent. CanvasApiClient applies Retry-After/backoff when
        // Canvas throttles. In daily mode only the submissions calls carry a since cutoff.
        var assignmentsTask = _canvas.GetAssignments(course.Id, cancellationToken);
        var groupsTask = _canvas.GetAssignmentGroups(course.Id, cancellationToken);
        var gradingPeriodsTask = _canvas.GetGradingPeriods(course.Id, cancellationToken);
        var enrollmentsTask = _canvas.GetEnrollments(course.Id, cancellationToken);
        var submissionsTask = _canvas.GetSubmissions(course.Id, cancellationToken);
        await Task.WhenAll(assignmentsTask, groupsTask, gradingPeriodsTask, enrollmentsTask, submissionsTask);

        var gradingPeriods = await gradingPeriodsTask;
        var gradingPeriodTasks = gradingPeriods.Select(async gradingPeriod =>
            new GradingPeriodEnrollments(
                gradingPeriod,
                await _canvas.GetEnrollments(course.Id, cancellationToken, gradingPeriod.Id)))
            .ToArray();

        return new CanvasCourseData(
            course,
            await assignmentsTask,
            await groupsTask,
            await enrollmentsTask,
            (await Task.WhenAll(gradingPeriodTasks)).ToList(),
            await submissionsTask);
    }

    private async Task<SyncCounts> SaveCourse(
        CanvasCourseDto sourceCourse,
        List<CanvasAssignmentDto> sourceAssignments,
        List<CanvasAssignmentGroupDto> sourceGroups,
        List<CanvasEnrollmentDto> sourceEnrollments,
        List<GradingPeriodEnrollments> gradingPeriodEnrollments,
        List<CanvasSubmissionDto> sourceSubmissions,
        bool syncActivityDetails,
        bool pruneMissingSubmissions,
        IReadOnlyDictionary<string, StudentProfile> studentsByCode,
        IReadOnlyDictionary<long, StudentProfile> studentsByCanvasId,
        IReadOnlyDictionary<long, string> sisUserIdByCanvasId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var course = await _db.CanvasCourses
            .SingleOrDefaultAsync(item => item.CanvasCourseId == sourceCourse.Id, cancellationToken);
        if (course is null)
        {
            course = new CanvasCourse
            {
                CanvasCourseId = sourceCourse.Id,
                CreatedAt = now
            };
            _db.CanvasCourses.Add(course);
        }
        else
        {
            course.UpdatedAt = now;
        }

        course.SisCourseId = sourceCourse.SisCourseId;
        course.Name = sourceCourse.Name;
        course.CourseCode = sourceCourse.CourseCode;
        course.WorkflowState = sourceCourse.WorkflowState;
        course.EnrollmentTermId = sourceCourse.EnrollmentTermId ?? sourceCourse.Term?.Id;
        course.EnrollmentTermName = sourceCourse.Term?.Name;
        course.StartAt = sourceCourse.StartAt;
        course.EndAt = sourceCourse.EndAt;

        var assignments = await _db.CanvasAssignments
            .Where(item => item.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasAssignmentId, cancellationToken);
        var assignmentGroupNames = sourceGroups.ToDictionary(item => item.Id, item => item.Name);
        foreach (var source in sourceAssignments)
        {
            if (!assignments.TryGetValue(source.Id, out var assignment))
            {
                assignment = new CanvasAssignment
                {
                    CanvasAssignmentId = source.Id,
                    CanvasCourseId = sourceCourse.Id,
                    Course = course,
                    CreatedAt = now
                };
                assignments.Add(source.Id, assignment);
                _db.CanvasAssignments.Add(assignment);
            }
            else
            {
                assignment.UpdatedAt = now;
            }

            assignment.Name = source.Name;
            assignment.PointsPossible = source.PointsPossible;
            assignment.DueAt = source.DueAt;
            assignment.WorkflowState = source.WorkflowState;
            assignment.AssignmentGroupId = source.AssignmentGroupId;
            assignment.AssignmentGroupName = source.AssignmentGroupId is { } groupId &&
                assignmentGroupNames.TryGetValue(groupId, out var groupName) ? groupName : null;
            assignment.GradingType = source.GradingType;
        }

        var enrollments = await _db.CanvasEnrollments
            .Where(item => item.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasEnrollmentId, cancellationToken);
        var sisIdByUser = new Dictionary<long, string>();
        var unmatchedStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourceEnrollments)
        {
            var sisUserId = FirstNotBlank(
                source.User?.SisUserId,
                source.SisUserId,
                sisUserIdByCanvasId.GetValueOrDefault(source.UserId));
            if (sisUserId is null)
            {
                unmatchedStudentIds.Add($"canvas-user:{source.UserId}");
            }
            var student = ResolveStudent(
                source.UserId, sisUserId, studentsByCanvasId, studentsByCode, unmatchedStudentIds);
            if (!enrollments.TryGetValue(source.Id, out var enrollment))
            {
                enrollment = new CanvasEnrollment
                {
                    CanvasEnrollmentId = source.Id,
                    CanvasCourseId = sourceCourse.Id,
                    Course = course,
                    CreatedAt = now
                };
                enrollments.Add(source.Id, enrollment);
                _db.CanvasEnrollments.Add(enrollment);
            }
            else
            {
                enrollment.UpdatedAt = now;
            }

            enrollment.CanvasUserId = source.UserId;
            enrollment.Type = source.Type;
            enrollment.EnrollmentState = source.EnrollmentState;
            enrollment.SisUserId = sisUserId;
            enrollment.StudentProfileId = student?.Id;
            enrollment.Student = student;
            if (sisUserId is not null)
            {
                sisIdByUser[source.UserId] = sisUserId;
            }
        }

        var existingScores = await _db.CanvasCourseScores
            .Where(item => enrollments.Keys.Contains(item.CanvasEnrollmentId))
            .ToDictionaryAsync(item => item.WarehouseScoreFactId, cancellationToken);
        var scoreSources = sourceEnrollments;
        var currentScoreIds = new HashSet<long>();
        foreach (var source in scoreSources)
        {
            var enrollment = enrollments[source.Id];
            var scoreId = StableFactId(source.Id, 1);
            currentScoreIds.Add(scoreId);
            if (!existingScores.TryGetValue(scoreId, out var score))
            {
                score = new CanvasCourseScore
                {
                    WarehouseScoreFactId = scoreId,
                    CanvasEnrollmentId = source.Id,
                    Enrollment = enrollment,
                    CreatedAt = now
                };
                existingScores.Add(scoreId, score);
                _db.CanvasCourseScores.Add(score);
            }
            else
            {
                score.UpdatedAt = now;
            }

            score.CanvasEnrollmentId = enrollment.CanvasEnrollmentId;
            score.Enrollment = enrollment;
            score.StudentProfileId = enrollment.StudentProfileId;
            score.Student = enrollment.Student;
            score.GradingPeriodId = null;
            score.GradingPeriodTitle = null;
            score.GradingPeriodStartDate = null;
            score.GradingPeriodEndDate = null;
            score.GradingPeriodWeight = null;
            score.CurrentScore = source.Grades?.CurrentScore;
            // Preserve Canvas's raw final_score exactly, including zero. Whether a zero with no
            // other grading activity is meaningful belongs in reporting logic, not ingestion.
            score.FinalScore = source.Grades?.FinalScore;
            score.EnrollmentOverrideScore = source.OverrideScore;
            score.GradesOverrideScore = source.Grades?.OverrideScore;
        }

        var gradingPeriodScoreCount = 0;
        foreach (var periodResult in gradingPeriodEnrollments)
        {
            var periodTitle = periodResult.Period.Title;
            var periodScoreSources = periodResult.Enrollments
                .Where(source => enrollments.ContainsKey(source.Id) && HasAnyCanvasScore(source))
                .ToList();

            foreach (var source in periodScoreSources)
            {
                var enrollment = enrollments[source.Id];
                var scoreId = StableFactId(source.Id, periodResult.Period.Id, 2);
                currentScoreIds.Add(scoreId);
                if (!existingScores.TryGetValue(scoreId, out var score))
                {
                    score = new CanvasCourseScore
                    {
                        WarehouseScoreFactId = scoreId,
                        CanvasEnrollmentId = enrollment.CanvasEnrollmentId,
                        Enrollment = enrollment,
                        CreatedAt = now
                    };
                    existingScores.Add(scoreId, score);
                    _db.CanvasCourseScores.Add(score);
                }
                else
                {
                    score.UpdatedAt = now;
                }

                score.CanvasEnrollmentId = enrollment.CanvasEnrollmentId;
                score.Enrollment = enrollment;
                score.StudentProfileId = enrollment.StudentProfileId;
                score.Student = enrollment.Student;
                score.GradingPeriodId = periodResult.Period.Id;
                score.GradingPeriodTitle = periodTitle;
                score.GradingPeriodStartDate = periodResult.Period.StartDate;
                score.GradingPeriodEndDate = periodResult.Period.EndDate;
                score.GradingPeriodWeight = periodResult.Period.Weight;
                score.CurrentScore = source.Grades?.CurrentScore;
                // Preserve Canvas's raw final_score exactly, including zero.
                score.FinalScore = source.Grades?.FinalScore;
                score.EnrollmentOverrideScore = source.OverrideScore;
                score.GradesOverrideScore = source.Grades?.OverrideScore;
                gradingPeriodScoreCount++;
            }
        }

        var existingSubmissions = await _db.CanvasSubmissions
            .Where(item => item.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasSubmissionId, cancellationToken);
        var currentSubmissionIds = new HashSet<long>();
        foreach (var source in sourceSubmissions.Where(item =>
                     item.Id.HasValue && assignments.ContainsKey(item.AssignmentId)))
        {
            var canvasSubmissionId = source.Id!.Value;
            currentSubmissionIds.Add(canvasSubmissionId);
            var sisUserId = FirstNotBlank(source.User?.SisUserId,
                sisIdByUser.GetValueOrDefault(source.UserId),
                sisUserIdByCanvasId.GetValueOrDefault(source.UserId));
            if (sisUserId is null)
            {
                unmatchedStudentIds.Add($"canvas-user:{source.UserId}");
            }
            var student = ResolveStudent(
                source.UserId, sisUserId, studentsByCanvasId, studentsByCode, unmatchedStudentIds);
            if (!existingSubmissions.TryGetValue(canvasSubmissionId, out var submission))
            {
                submission = new CanvasSubmission
                {
                    CanvasSubmissionId = canvasSubmissionId,
                    CanvasCourseId = sourceCourse.Id,
                    CanvasAssignmentId = source.AssignmentId,
                    Assignment = assignments[source.AssignmentId],
                    CreatedAt = now
                };
                existingSubmissions.Add(canvasSubmissionId, submission);
                _db.CanvasSubmissions.Add(submission);
            }
            else
            {
                submission.UpdatedAt = now;
            }

            submission.CanvasUserId = source.UserId;
            submission.StudentProfileId = student?.Id;
            submission.Student = student;
            submission.Score = source.Score;
            submission.Grade = source.Grade;
            submission.WorkflowState = source.WorkflowState;
            submission.SubmissionType = source.SubmissionType;
            submission.SubmittedAt = source.SubmittedAt;
            submission.GradedAt = source.GradedAt;
            submission.CachedDueDate = source.CachedDueDate;
            submission.Late = source.Late;
            submission.Missing = source.Missing;
            submission.Excused = source.Excused;
            submission.SecondsLate = source.SecondsLate;
        }

        // These course-scoped endpoints are authoritative. Removing absent children prevents stale
        // grades and assignments from continuing to influence dashboard results.
        var assignmentIds = sourceAssignments.Select(item => item.Id).ToHashSet();
        var enrollmentIds = sourceEnrollments.Select(item => item.Id).ToHashSet();
        if (syncActivityDetails)
        {
            if (pruneMissingSubmissions)
            {
                _db.CanvasSubmissions.RemoveRange(existingSubmissions.Values
                    .Where(item => !currentSubmissionIds.Contains(item.CanvasSubmissionId)));
            }
            _db.CanvasAssignments.RemoveRange(assignments.Values
                .Where(item => !assignmentIds.Contains(item.CanvasAssignmentId)));
        }
        _db.CanvasCourseScores.RemoveRange(existingScores.Values
            .Where(item => !currentScoreIds.Contains(item.WarehouseScoreFactId)));
        _db.CanvasEnrollments.RemoveRange(enrollments.Values
            .Where(item => !enrollmentIds.Contains(item.CanvasEnrollmentId)));

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return new SyncCounts
        {
            Assignments = sourceAssignments.Count,
            Enrollments = sourceEnrollments.Count,
            Scores = scoreSources.Count + gradingPeriodScoreCount,
            Submissions = currentSubmissionIds.Count,
            UnmatchedStudents = unmatchedStudentIds.Count,
            ScoresWithoutGrades = scoreSources.Count(source => !HasAnyCanvasScore(source)),
            StudentUsers = scoreSources.Count,
            MatchedStudents = scoreSources.Count(source => enrollments[source.Id].StudentProfileId is not null)
        };
    }

    private async Task<StudentSyncCounts> LinkSISStudents(
        List<CanvasUserDto> sourceStudents,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var profiles = await _db.StudentProfiles.ToListAsync(cancellationToken);
        var byCanvasId = profiles
            .Where(student => student.CanvasUserId is not null)
            .ToDictionary(student => student.CanvasUserId!.Value);
        var bySISUserId = profiles
            .Where(student => !string.IsNullOrWhiteSpace(student.SISUserId))
            .ToDictionary(student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
        var counts = new StudentSyncCounts();

        foreach (var source in sourceStudents
            .GroupBy(student => student.Id)
            .Select(group => group
                .OrderByDescending(student => !string.IsNullOrWhiteSpace(student.SisUserId))
                .ThenByDescending(student => !string.IsNullOrWhiteSpace(student.Name))
                .First())
            .OrderBy(student => student.Id))
        {
            var sisUserId = Limit(source.SisUserId, 100);
            if (sisUserId is null)
            {
                counts.MissingSISUserIds++;
                continue;
            }

            if (!bySISUserId.TryGetValue(sisUserId, out var profile))
            {
                counts.Unmatched++;
                continue;
            }

            counts.Matched++;
            if (byCanvasId.TryGetValue(source.Id, out var canvasMatch) && canvasMatch.Id != profile.Id)
            {
                throw IdentityConflict(
                    source,
                    $"Canvas user id is linked to SIS profile {canvasMatch.Id}, but sis_user_id '{sisUserId}' " +
                    $"matches SIS profile {profile.Id}");
            }

            if (profile.CanvasUserId is { } assignedCanvasId && assignedCanvasId != source.Id)
            {
                throw IdentityConflict(
                    source,
                    $"SIS profile {profile.Id} is already linked to Canvas user {assignedCanvasId}");
            }

            if (profile.CanvasUserId is null)
            {
                profile.CanvasUserId = source.Id;
                byCanvasId.Add(source.Id, profile);
                profile.UpdatedAt = DateTime.UtcNow;
                counts.CanvasLinksCreated++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return counts;
    }

    private async Task ReplaceCanvasWarehouse(CancellationToken cancellationToken)
    {
        Console.WriteLine("Explicit replace requested; removing existing Canvas warehouse rows.");
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.CanvasCourseScores.ExecuteDeleteAsync(cancellationToken);
        await _db.CanvasSubmissions.ExecuteDeleteAsync(cancellationToken);
        await _db.CanvasEnrollments.ExecuteDeleteAsync(cancellationToken);
        await _db.CanvasAssignments.ExecuteDeleteAsync(cancellationToken);
        await _db.CanvasCourses.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<int> RelinkExistingFacts(CancellationToken cancellationToken)
    {
        // SIS can arrive after Canvas history. Relink the entire warehouse independently of the
        // current course window so a daily run unlocks old transcripts as soon as SISUserId exists.
        var linked = await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasEnrollments` e
            INNER JOIN `StudentProfiles` p ON p.`CanvasUserId` = e.`CanvasUserId`
            SET e.`StudentProfileId` = p.`Id`, e.`UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE e.`StudentProfileId` IS NULL
            """, cancellationToken);
        linked += await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasEnrollments` e
            INNER JOIN `StudentProfiles` p ON p.`SISUserId` = e.`SisUserId`
            SET e.`StudentProfileId` = p.`Id`, e.`UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE e.`StudentProfileId` IS NULL AND e.`SisUserId` IS NOT NULL
            """, cancellationToken);
        linked += await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasSubmissions` s
            INNER JOIN `StudentProfiles` p ON p.`CanvasUserId` = s.`CanvasUserId`
            SET s.`StudentProfileId` = p.`Id`, s.`UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE s.`StudentProfileId` IS NULL
            """, cancellationToken);
        _db.ChangeTracker.Clear();
        return linked;
    }

    private static StudentProfile? ResolveStudent(
        long canvasUserId,
        string? sisUserId,
        IReadOnlyDictionary<long, StudentProfile> studentsByCanvasId,
        IReadOnlyDictionary<string, StudentProfile> studentsByCode,
        ISet<string> unmatchedStudentIds)
    {
        if (studentsByCanvasId.TryGetValue(canvasUserId, out var canvasStudent))
        {
            return canvasStudent;
        }

        if (sisUserId is not null && studentsByCode.TryGetValue(sisUserId, out var sisStudent))
        {
            return sisStudent;
        }

        unmatchedStudentIds.Add(sisUserId ?? $"canvas-user:{canvasUserId}");
        return null;
    }

    private static InvalidOperationException IdentityConflict(CanvasUserDto source, string detail) =>
        new($"Canvas student identity conflict for user {source.Id}: {detail}. Resolve it before rerunning the ETL.");

    private static string? FirstNotBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static long StableFactId(params long[] values)
    {
        const ulong offset = 14695981039346656037;
        const ulong prime = 1099511628211;
        var hash = offset;
        foreach (var value in values)
        {
            var bytes = BitConverter.GetBytes(value);
            foreach (var item in bytes)
            {
                hash ^= item;
                hash *= prime;
            }
        }

        return (long)(hash & long.MaxValue);
    }

    private static bool HasAnyCanvasScore(CanvasEnrollmentDto enrollment) =>
        enrollment.Grades?.CurrentScore is not null ||
        enrollment.Grades?.FinalScore is not null ||
        enrollment.OverrideScore is not null ||
        enrollment.Grades?.OverrideScore is not null;

    private static string? Limit(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static double Percent(int value, int total) => total == 0 ? 100 : value * 100d / total;

    private sealed class SyncCounts
    {
        public int Assignments { get; set; }
        public int Enrollments { get; set; }
        public int Scores { get; set; }
        public int Submissions { get; set; }
        public int UnmatchedStudents { get; set; }
        public int ScoresWithoutGrades { get; set; }
        public int StudentUsers { get; set; }
        public int MatchedStudents { get; set; }

        public void Add(SyncCounts other)
        {
            Assignments += other.Assignments;
            Enrollments += other.Enrollments;
            Scores += other.Scores;
            Submissions += other.Submissions;
            UnmatchedStudents += other.UnmatchedStudents;
            ScoresWithoutGrades += other.ScoresWithoutGrades;
            StudentUsers += other.StudentUsers;
            MatchedStudents += other.MatchedStudents;
        }
    }

    private sealed record GradingPeriodEnrollments(
        CanvasGradingPeriodDto Period,
        List<CanvasEnrollmentDto> Enrollments);

    private sealed record CanvasCourseData(
        CanvasCourseDto Course,
        List<CanvasAssignmentDto> Assignments,
        List<CanvasAssignmentGroupDto> Groups,
        List<CanvasEnrollmentDto> Enrollments,
        List<GradingPeriodEnrollments> GradingPeriodEnrollments,
        List<CanvasSubmissionDto> Submissions);

    private sealed class StudentSyncCounts
    {
        public int Matched { get; set; }
        public int CanvasLinksCreated { get; set; }
        public int Unmatched { get; set; }
        public int MissingSISUserIds { get; set; }
    }
}

public sealed record CanvasSyncSummary(
    int CanvasDirectoryUsers,
    int StudentProfilesMatched,
    int CanvasUserLinksCreated,
    int UnmatchedCanvasUsers,
    int Courses,
    int Assignments,
    int Enrollments,
    int Scores,
    int Submissions,
    int UnmatchedStudentReferences,
    int StudentUsers,
    int MatchedStudents)
{
    public double StudentMatchRate => StudentUsers == 0 ? 100 : MatchedStudents * 100d / StudentUsers;
}
