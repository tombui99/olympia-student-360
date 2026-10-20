using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
        if (_options.SinceUtc is { } sinceUtc)
        {
            Console.WriteLine(
                $"Incremental Canvas submission window starts at {sinceUtc:yyyy-MM-dd'T'HH:mm:ss'Z'}; " +
                "active-term enrollment grade totals will still be fully refreshed.");
        }

        var sourceAccounts = await _canvas.GetAccounts(cancellationToken);
        Console.WriteLine($"Canvas returned {sourceAccounts.Count} account/subaccount records.");
        var sourceStudents = await _canvas.GetStudents(cancellationToken);
        Console.WriteLine(
            $"Canvas returned {sourceStudents.Count} account student users.");
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

        var studentCounts = await LinkSISStudents(sourceStudents, cancellationToken);
        Console.WriteLine(
            $"Existing Canvas/SIS identity linking: {studentCounts.Matched} matched existing SIS profiles, " +
            $"{studentCounts.CanvasLinksCreated} Canvas user links created, " +
            $"{studentCounts.Unmatched} Canvas users unmatched, " +
            $"{studentCounts.MissingSISUserIds} Canvas users missing sis_user_id. " +
            "StudentProfiles remains authoritative; Canvas-only users are excluded.");

        var activeCanvasUserIds = (await _db.StudentProfiles.AsNoTracking()
                .Where(student => student.CanvasUserId != null)
                .Select(student => student.CanvasUserId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        if (activeCanvasUserIds.Count < 100 && !_options.AllowEmptyStudents)
        {
            throw new InvalidOperationException(
                $"Only {activeCanvasUserIds.Count} SIS StudentProfiles have a Canvas user id. " +
                "The active-roster safety check requires at least 100 before Canvas data can be pruned.");
        }
        var activeSourceStudents = sourceStudents
            .Where(student => activeCanvasUserIds.Contains(student.Id))
            .GroupBy(student => student.Id)
            .Select(group => group.First())
            .ToList();
        Console.WriteLine(
            $"SIS active roster retained {activeSourceStudents.Count} of {sourceStudents.Count} Canvas student users.");

        if (replaceExisting)
        {
            await ReplaceCanvasWarehouse(cancellationToken);
        }

        await SaveCanvasDirectory(sourceAccounts, activeSourceStudents, cancellationToken);

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
                .Where(course => deletedCourseIds.Contains(course.CanvasCourseId) &&
                    !course.Enrollments.Any(enrollment => enrollment.StudentProfileId == null) &&
                    !course.Assignments.Any(assignment =>
                        assignment.Submissions.Any(submission => submission.StudentProfileId == null)))
                .ExecuteDeleteAsync(cancellationToken);
            Console.WriteLine(
                $"Removed {deletedRows} Canvas courses that Canvas now reports as deleted; " +
                "courses containing inactive-student history were retained.");
        }

        courses = courses
            .Where(course => !string.Equals(course.WorkflowState, "deleted", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Retain scalar identities, never EF entities, between course transactions.
        // Reattaching cached StudentProfile navigation objects after Clear() can
        // overwrite newer SIS data and collide with risk-report FK locks.
        var studentIdentities = await _db.StudentProfiles.AsNoTracking()
            .Select(student => new StudentIdentity(student.Id, student.CanvasUserId, student.SISUserId))
            .ToListAsync(cancellationToken);
        var studentsByCode = studentIdentities
            .Where(student => !string.IsNullOrWhiteSpace(student.SISUserId))
            .ToDictionary(
                student => StudentIdentityNormalizer.SchoolUsername(student.SISUserId)!,
                StringComparer.OrdinalIgnoreCase);
        var studentsByCanvasId = studentIdentities
            .Where(student => student.CanvasUserId != null)
            .ToDictionary(student => student.CanvasUserId!.Value);
        var sisUserIdByCanvasId = sourceStudents
            .Select(student => new
            {
                student.Id,
                SISUserId = StudentIdentityNormalizer.SchoolUsername(
                    FirstNotBlank(student.SisUserId, student.LoginId, student.Email))
            })
            .Where(student => student.SISUserId is not null)
            .GroupBy(student => student.Id)
            .ToDictionary(
                group => group.Key,
                group => group.Select(student => student.SISUserId!).First());
        var accountsById = sourceAccounts.ToDictionary(account => account.Id);
        var totals = new SyncCounts();
        var retainedCourseCount = 0;
        // Canvas throttles large bursts heavily. Keep full/reconciliation reads bounded and use an
        // even smaller active-course batch for daily runs. Submission payloads remain watermark
        // filtered in incremental mode; dimensions and enrollment grade totals are refreshed.
        var courseBatchSize = _options.IncludeCompletedCourses ? 8 : 4;
        var processedCourses = 0;
        var elapsed = Stopwatch.StartNew();
        foreach (var courseBatch in courses.Chunk(courseBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine(
                $"Fetching Canvas course batch starting at {courseBatch[0].Id} " +
                $"({courseBatch.Length} courses).");
            var courseData = await Task.WhenAll(courseBatch.Select(course =>
                FetchCourseData(course, activeCanvasUserIds, cancellationToken)));
            foreach (var data in courseData)
            {
                if (!data.HasActiveStudents)
                {
                    continue;
                }

                retainedCourseCount++;
                Console.WriteLine($"Syncing Canvas course {data.Course.Id}: {data.Course.Name ?? "(unnamed)"}");
                var counts = await SaveCourse(
                    data.Course,
                    data.Sections,
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
                    accountsById,
                    cancellationToken);
                totals.Add(counts);
            }
            processedCourses += courseBatch.Length;
            Console.WriteLine($"Canvas course progress: {processedCourses}/{courses.Count} checked, {retainedCourseCount} imported; elapsed {elapsed.Elapsed:hh\\:mm\\:ss}.");
        }

        var canvasGradeCounts = await UpdateCanvasStudentGradeLevels(cancellationToken);
        Console.WriteLine(
            $"Canvas grade fallback updated for {canvasGradeCounts.StudentsWithGrade} students; " +
            $"{canvasGradeCounts.StudentsWithMultipleGrades} had memberships under multiple grade accounts " +
            "and were assigned the grade backed by the most course enrollments.");

        Console.WriteLine(
            $"Canvas data import finished: {retainedCourseCount} courses with SIS-active students, " +
            $"{totals.Sections} Canvas classes/sections, " +
            $"{totals.Assignments} assignments, " +
            $"{totals.Enrollments} enrollments, {totals.Scores} course scores, {totals.Submissions} submissions. " +
            $"Unmatched SIS student references: {totals.UnmatchedStudents}; " +
            $"score rows without grades: {totals.ScoresWithoutGrades}; " +
            $"student match rate: {Percent(totals.MatchedStudents, totals.StudentUsers):0.0}%.");

        return new CanvasSyncSummary(
            activeSourceStudents.Count,
            studentCounts.Matched,
            studentCounts.CanvasLinksCreated,
            studentCounts.Unmatched,
            sourceAccounts.Count,
            retainedCourseCount,
            totals.Sections,
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
        IReadOnlySet<long> activeCanvasUserIds,
        CancellationToken cancellationToken)
    {
        // Enrollment is read first so courses and submission payloads can be restricted to the
        // SIS-authoritative active roster before the heavier course-scoped calls are made.
        var sourceEnrollments = await _canvas.GetEnrollments(course.Id, cancellationToken);
        var activeEnrollments = sourceEnrollments
            .Where(enrollment => activeCanvasUserIds.Contains(enrollment.UserId))
            .ToList();
        if (activeEnrollments.Count == 0)
        {
            return new CanvasCourseData(course, [], [], [], [], [], [], false);
        }

        // Remaining course-scoped reads are independent. CanvasApiClient applies Retry-After/backoff
        // when Canvas throttles. In daily mode only the submissions calls carry a since cutoff.
        var assignmentsTask = _canvas.GetAssignments(course.Id, cancellationToken);
        var sectionsTask = _canvas.GetSections(course.Id, cancellationToken);
        var groupsTask = _canvas.GetAssignmentGroups(course.Id, cancellationToken);
        var gradingPeriodsTask = _canvas.GetGradingPeriods(course.Id, cancellationToken);
        var submissionsTask = _canvas.GetSubmissions(
            course.Id,
            activeEnrollments.Select(enrollment => enrollment.UserId).Distinct().ToArray(),
            cancellationToken);
        await Task.WhenAll(
            assignmentsTask, sectionsTask, groupsTask, gradingPeriodsTask, submissionsTask);

        var gradingPeriods = (await gradingPeriodsTask)
            .Where(period => GradingPeriodOverlapsCourseTerm(period, course))
            .ToList();
        var gradingPeriodTasks = gradingPeriods.Select(async gradingPeriod =>
            new GradingPeriodEnrollments(
                gradingPeriod,
                await _canvas.GetEnrollments(course.Id, cancellationToken, gradingPeriod.Id)))
            .ToArray();

        return new CanvasCourseData(
            course,
            await sectionsTask,
            await assignmentsTask,
            await groupsTask,
            activeEnrollments,
            (await Task.WhenAll(gradingPeriodTasks)).ToList(),
            await submissionsTask,
            true);
    }

    private async Task<SyncCounts> SaveCourse(
        CanvasCourseDto sourceCourse,
        List<CanvasCourseSectionDto> sourceSections,
        List<CanvasAssignmentDto> sourceAssignments,
        List<CanvasAssignmentGroupDto> sourceGroups,
        List<CanvasEnrollmentDto> sourceEnrollments,
        List<GradingPeriodEnrollments> gradingPeriodEnrollments,
        List<CanvasSubmissionDto> sourceSubmissions,
        bool syncActivityDetails,
        bool pruneMissingSubmissions,
        IReadOnlyDictionary<string, StudentIdentity> studentsByCode,
        IReadOnlyDictionary<long, StudentIdentity> studentsByCanvasId,
        IReadOnlyDictionary<long, string> sisUserIdByCanvasId,
        IReadOnlyDictionary<long, CanvasAccountDto> accountsById,
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
        course.CanvasAccountId = sourceCourse.AccountId;
        course.CanvasRootAccountId = sourceCourse.RootAccountId;
        var grade = ResolveCanvasGrade(sourceCourse.AccountId, accountsById);
        course.CanvasGradeLevel = grade?.GradeLevel;
        course.CanvasGradeSourceAccountId = grade?.AccountId;
        course.EnrollmentTermId = sourceCourse.EnrollmentTermId ?? sourceCourse.Term?.Id;
        course.EnrollmentTermName = sourceCourse.Term?.Name;
        // Most Olympia courses inherit dates from their enrollment term and leave the course-level
        // fields null. Keep the effective dates so reporting and grading-period selection use the
        // actual Canvas term rather than the ETL execution year.
        course.StartAt = sourceCourse.StartAt ?? sourceCourse.Term?.StartAt;
        course.EndAt = sourceCourse.EndAt ?? sourceCourse.Term?.EndAt;

        var sections = await _db.CanvasCourseSections
            .Where(item => item.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasSectionId, cancellationToken);
        foreach (var source in sourceSections)
        {
            if (!sections.TryGetValue(source.Id, out var section))
            {
                section = new CanvasCourseSection
                {
                    CanvasSectionId = source.Id,
                    CanvasCourseId = sourceCourse.Id,
                    Course = course,
                    CreatedAt = now
                };
                sections.Add(source.Id, section);
                _db.CanvasCourseSections.Add(section);
            }
            else
            {
                section.UpdatedAt = now;
            }

            section.SisSectionId = source.SisSectionId;
            section.SisCourseId = source.SisCourseId;
            section.IntegrationId = source.IntegrationId;
            section.Name = source.Name;
            section.StartAt = source.StartAt;
            section.EndAt = source.EndAt;
            section.TotalStudents = sourceEnrollments
                .Where(enrollment => enrollment.CourseSectionId == source.Id)
                .Select(enrollment => enrollment.UserId)
                .Distinct()
                .Count();
            section.RestrictEnrollmentsToSectionDates = source.RestrictEnrollmentsToSectionDates;
        }

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
            assignment.UnlockAt = source.UnlockAt;
            assignment.LockAt = source.LockAt;
            assignment.Published = source.Published;
            assignment.OmitFromFinalGrade = source.OmitFromFinalGrade;
            assignment.Position = source.Position;
        }

        var enrollments = await _db.CanvasEnrollments
            .Where(item => item.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasEnrollmentId, cancellationToken);
        var sisIdByUser = new Dictionary<long, string>();
        var unmatchedStudentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retainedSourceEnrollments = new List<CanvasEnrollmentDto>();

        foreach (var source in sourceEnrollments)
        {
            var sourceSisUserId = FirstNotBlank(
                source.User?.SisUserId,
                source.SisUserId,
                sisUserIdByCanvasId.GetValueOrDefault(source.UserId));
            var sisUserId = StudentIdentityNormalizer.SchoolUsername(sourceSisUserId);
            var student = ResolveStudent(
                source.UserId,
                sisUserId,
                studentsByCanvasId,
                studentsByCode,
                unmatchedStudentIds);
            if (student is null)
            {
                // Once SIS owns the active roster, unmatched Canvas users are intentionally not
                // materialized as student facts. This keeps former/inactive students out of 360.
                continue;
            }
            retainedSourceEnrollments.Add(source);
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
            enrollment.Role = source.Role;
            enrollment.RoleId = source.RoleId;
            enrollment.EnrollmentState = source.EnrollmentState;
            enrollment.SisUserId = sourceSisUserId;
            enrollment.CanvasCourseSectionId = source.CourseSectionId;
            enrollment.Section = source.CourseSectionId is { } sectionId &&
                sections.TryGetValue(sectionId, out var section) ? section : null;
            enrollment.SisSectionId = source.SisSectionId;
            enrollment.SisCourseId = source.SisCourseId;
            enrollment.SisAccountId = source.SisAccountId;
            enrollment.SourceCreatedAt = source.CreatedAt;
            enrollment.SourceUpdatedAt = source.UpdatedAt;
            enrollment.StartAt = source.StartAt;
            enrollment.EndAt = source.EndAt;
            enrollment.LastActivityAt = source.LastActivityAt;
            enrollment.LastAttendedAt = source.LastAttendedAt;
            enrollment.TotalActivityTimeSeconds = source.TotalActivityTime;
            enrollment.LimitPrivilegesToCourseSection = source.LimitPrivilegesToCourseSection;
            enrollment.StudentProfileId = student?.Id;
            if (sisUserId is not null)
            {
                sisIdByUser[source.UserId] = sisUserId;
            }
        }

        var existingScores = await _db.CanvasCourseScores
            .Where(item => enrollments.Keys.Contains(item.CanvasEnrollmentId))
            .ToDictionaryAsync(item => item.WarehouseScoreFactId, cancellationToken);
        var scoreSources = retainedSourceEnrollments;
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
            score.GradingPeriodId = null;
            score.GradingPeriodTitle = null;
            score.GradingPeriodStartDate = null;
            score.GradingPeriodEndDate = null;
            score.GradingPeriodWeight = null;
            score.CurrentScore = source.Grades?.CurrentScore;
            // Preserve Canvas's raw final_score exactly, including zero. Whether a zero with no
            // other grading activity is meaningful belongs in reporting logic, not ingestion.
            score.FinalScore = source.Grades?.FinalScore;
            score.CurrentPoints = source.Grades?.CurrentPoints;
            score.CurrentGrade = source.Grades?.CurrentGrade;
            score.FinalGrade = source.Grades?.FinalGrade;
            score.UnpostedCurrentScore = source.Grades?.UnpostedCurrentScore;
            score.UnpostedFinalScore = source.Grades?.UnpostedFinalScore;
            score.UnpostedCurrentPoints = source.Grades?.UnpostedCurrentPoints;
            score.UnpostedCurrentGrade = source.Grades?.UnpostedCurrentGrade;
            score.UnpostedFinalGrade = source.Grades?.UnpostedFinalGrade;
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
                score.GradingPeriodId = periodResult.Period.Id;
                score.GradingPeriodTitle = periodTitle;
                score.GradingPeriodStartDate = periodResult.Period.StartDate;
                score.GradingPeriodEndDate = periodResult.Period.EndDate;
                score.GradingPeriodWeight = periodResult.Period.Weight;
                score.CurrentScore = source.Grades?.CurrentScore;
                // Preserve Canvas's raw final_score exactly, including zero.
                score.FinalScore = source.Grades?.FinalScore;
                score.CurrentPoints = source.Grades?.CurrentPoints;
                score.CurrentGrade = source.Grades?.CurrentGrade;
                score.FinalGrade = source.Grades?.FinalGrade;
                score.UnpostedCurrentScore = source.Grades?.UnpostedCurrentScore;
                score.UnpostedFinalScore = source.Grades?.UnpostedFinalScore;
                score.UnpostedCurrentPoints = source.Grades?.UnpostedCurrentPoints;
                score.UnpostedCurrentGrade = source.Grades?.UnpostedCurrentGrade;
                score.UnpostedFinalGrade = source.Grades?.UnpostedFinalGrade;
                score.EnrollmentOverrideScore = source.OverrideScore;
                score.GradesOverrideScore = source.Grades?.OverrideScore;
                gradingPeriodScoreCount++;
            }
        }

        var existingSubmissions = await _db.CanvasSubmissions
            // Use the indexed assignment join instead of scanning all submissions for
            // every course on the denormalized, unindexed CanvasCourseId column.
            .Where(item => item.Assignment!.CanvasCourseId == sourceCourse.Id)
            .ToDictionaryAsync(item => item.CanvasSubmissionId, cancellationToken);
        var currentSubmissionIds = new HashSet<long>();
        foreach (var source in sourceSubmissions.Where(item =>
                     item.Id.HasValue && assignments.ContainsKey(item.AssignmentId)))
        {
            var canvasSubmissionId = source.Id!.Value;
            var sisUserId = StudentIdentityNormalizer.SchoolUsername(FirstNotBlank(source.User?.SisUserId,
                sisIdByUser.GetValueOrDefault(source.UserId),
                sisUserIdByCanvasId.GetValueOrDefault(source.UserId)));
            var student = ResolveStudent(
                source.UserId,
                sisUserId,
                studentsByCanvasId,
                studentsByCode,
                unmatchedStudentIds);
            if (student is null)
            {
                continue;
            }
            currentSubmissionIds.Add(canvasSubmissionId);
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
            submission.Attempt = source.Attempt;
            submission.PostedAt = source.PostedAt;
            submission.GraderId = source.GraderId;
            submission.PointsDeducted = source.PointsDeducted;
            submission.LatePolicyStatus = source.LatePolicyStatus;
        }

        // These course-scoped endpoints are authoritative. Removing absent children prevents stale
        // grades and assignments from continuing to influence dashboard results.
        var assignmentIds = sourceAssignments.Select(item => item.Id).ToHashSet();
        var sectionIds = sourceSections.Select(item => item.Id).ToHashSet();
        var enrollmentIds = retainedSourceEnrollments.Select(item => item.Id).ToHashSet();
        if (syncActivityDetails)
        {
            if (pruneMissingSubmissions)
            {
                _db.CanvasSubmissions.RemoveRange(existingSubmissions.Values
                    .Where(item => item.StudentProfileId != null &&
                        !currentSubmissionIds.Contains(item.CanvasSubmissionId)));
            }
            var assignmentsWithInactiveHistory = existingSubmissions.Values
                .Where(submission => submission.StudentProfileId == null)
                .Select(submission => submission.CanvasAssignmentId)
                .ToHashSet();
            _db.CanvasAssignments.RemoveRange(assignments.Values
                .Where(item => !assignmentIds.Contains(item.CanvasAssignmentId) &&
                    !assignmentsWithInactiveHistory.Contains(item.CanvasAssignmentId)));
        }
        _db.CanvasCourseScores.RemoveRange(existingScores.Values
            .Where(item => item.StudentProfileId != null &&
                !currentScoreIds.Contains(item.WarehouseScoreFactId)));
        _db.CanvasEnrollments.RemoveRange(enrollments.Values
            .Where(item => item.StudentProfileId != null &&
                !enrollmentIds.Contains(item.CanvasEnrollmentId)));
        _db.CanvasCourseSections.RemoveRange(sections.Values
            .Where(item => !sectionIds.Contains(item.CanvasSectionId)));

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return new SyncCounts
        {
            Sections = sourceSections.Count,
            Assignments = sourceAssignments.Count,
            Enrollments = retainedSourceEnrollments.Count,
            Scores = scoreSources.Count + gradingPeriodScoreCount,
            Submissions = currentSubmissionIds.Count,
            UnmatchedStudents = unmatchedStudentIds.Count,
            ScoresWithoutGrades = scoreSources.Count(source => !HasAnyCanvasScore(source)),
            StudentUsers = scoreSources.Count,
            MatchedStudents = scoreSources.Count(source => enrollments[source.Id].StudentProfileId is not null)
        };
    }

    private async Task SaveCanvasDirectory(
        List<CanvasAccountDto> sourceAccounts,
        List<CanvasUserDto> sourceUsers,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var accounts = await _db.CanvasAccounts
            .ToDictionaryAsync(item => item.CanvasAccountId, cancellationToken);
        foreach (var source in sourceAccounts)
        {
            if (!accounts.TryGetValue(source.Id, out var account))
            {
                account = new CanvasAccount { CanvasAccountId = source.Id, CreatedAt = now };
                accounts.Add(source.Id, account);
                _db.CanvasAccounts.Add(account);
            }
            else
            {
                account.UpdatedAt = now;
            }

            account.ParentAccountId = source.ParentAccountId;
            account.RootAccountId = source.RootAccountId;
            account.SisAccountId = Limit(source.SisAccountId, 255);
            account.Name = Limit(source.Name, 255);
            account.WorkflowState = Limit(source.WorkflowState, 50);
        }

        var users = await _db.CanvasUsers
            .ToDictionaryAsync(item => item.CanvasUserId, cancellationToken);
        foreach (var source in sourceUsers.GroupBy(item => item.Id).Select(group => group.First()))
        {
            if (!users.TryGetValue(source.Id, out var user))
            {
                user = new CanvasUser { CanvasUserId = source.Id, CreatedAt = now };
                users.Add(source.Id, user);
                _db.CanvasUsers.Add(user);
            }
            else
            {
                user.UpdatedAt = now;
            }

            user.SisUserId = Limit(source.SisUserId, 100);
            user.IntegrationId = Limit(source.IntegrationId, 255);
            user.LoginId = Limit(source.LoginId, 255);
            user.Name = Limit(source.Name, 255);
            user.ShortName = Limit(source.ShortName, 255);
            user.SortableName = Limit(source.SortableName, 255);
            user.Email = Limit(source.Email, 255);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _db.ChangeTracker.Clear();
    }

    private async Task<CanvasGradeCounts> UpdateCanvasStudentGradeLevels(CancellationToken cancellationToken)
    {
        var memberships = await _db.CanvasEnrollments.AsNoTracking()
            .Where(item => item.StudentProfileId != null &&
                item.Course!.CanvasGradeLevel != null && item.Course.CanvasGradeLevel != "")
            .GroupBy(item => new { StudentId = item.StudentProfileId!.Value, item.Course!.CanvasGradeLevel })
            .Select(group => new
            {
                group.Key.StudentId,
                GradeLevel = group.Key.CanvasGradeLevel!,
                CourseCount = group.Count()
            })
            .ToListAsync(cancellationToken);
        var byStudent = memberships.GroupBy(item => item.StudentId).ToDictionary(
            group => group.Key,
            group => group.OrderByDescending(item => item.CourseCount)
                .ThenByDescending(item => int.TryParse(item.GradeLevel, out var grade) ? grade : -1)
                .First().GradeLevel);
        var multipleGrades = memberships.GroupBy(item => item.StudentId).Count(group => group.Count() > 1);
        var profiles = await _db.StudentProfiles
            .Where(item => item.CanvasUserId != null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var profile in profiles)
        {
            var grade = byStudent.GetValueOrDefault(profile.Id);
            if (!string.Equals(profile.CanvasGradeLevel, grade, StringComparison.Ordinal))
            {
                profile.CanvasGradeLevel = grade;
                profile.UpdatedAt = now;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return new CanvasGradeCounts(byStudent.Count, multipleGrades);
    }

    private static CanvasGrade? ResolveCanvasGrade(
        long? courseAccountId,
        IReadOnlyDictionary<long, CanvasAccountDto> accountsById)
    {
        var accountId = courseAccountId;
        var visited = new HashSet<long>();
        while (accountId is { } id && visited.Add(id) && accountsById.TryGetValue(id, out var account))
        {
            var grade = GradeFromCanvasAccount(account);
            if (grade is not null)
            {
                return new CanvasGrade(grade, account.Id);
            }
            accountId = account.ParentAccountId;
        }
        return null;
    }

    private static string? GradeFromCanvasAccount(CanvasAccountDto account)
    {
        var sisMatch = Regex.Match(account.SisAccountId ?? "",
            @"(?:^|_)(?:KHOI|GRADE)0*(?<grade>1[0-2]|[1-9])(?:_|$)", RegexOptions.IgnoreCase);
        var nameMatch = Regex.Match(account.Name ?? "",
            @"(?:Grade|Khối)\s*0*(?<grade>1[0-2]|[1-9])\b", RegexOptions.IgnoreCase);
        var value = sisMatch.Success ? sisMatch.Groups["grade"].Value :
            nameMatch.Success ? nameMatch.Groups["grade"].Value : null;
        return int.TryParse(value, out var grade) && grade is >= 1 and <= 12
            ? grade.ToString()
            : null;
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
            .ToDictionary(
                student => StudentIdentityNormalizer.SchoolUsername(student.SISUserId)!,
                StringComparer.OrdinalIgnoreCase);
        var counts = new StudentSyncCounts();

        foreach (var source in sourceStudents
            .GroupBy(student => student.Id)
            .Select(group => group
                .OrderByDescending(student => !string.IsNullOrWhiteSpace(student.SisUserId))
                .ThenByDescending(student => !string.IsNullOrWhiteSpace(student.Name))
                .First())
            .OrderBy(student => student.Id))
        {
            var sisUserId = StudentIdentityNormalizer.SchoolUsername(
                FirstNotBlank(source.SisUserId, source.LoginId, source.Email));
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
            SET e.`StudentProfileId` = p.`Id`, e.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE e.`StudentProfileId` IS NULL
            """, cancellationToken);
        linked += await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasEnrollments` e
            INNER JOIN `StudentProfiles` p
              ON p.`SISUserId` = LOWER(SUBSTRING_INDEX(TRIM(e.`SisUserId`), '@', 1))
            SET e.`StudentProfileId` = p.`Id`, e.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE e.`StudentProfileId` IS NULL AND e.`SisUserId` IS NOT NULL
            """, cancellationToken);
        linked += await _db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasSubmissions` s
            INNER JOIN `StudentProfiles` p ON p.`CanvasUserId` = s.`CanvasUserId`
            SET s.`StudentProfileId` = p.`Id`, s.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE s.`StudentProfileId` IS NULL
            """, cancellationToken);
        _db.ChangeTracker.Clear();
        return linked;
    }

    private sealed record StudentIdentity(int Id, long? CanvasUserId, string SISUserId);

    private static StudentIdentity? ResolveStudent(
        long canvasUserId,
        string? sisUserId,
        IReadOnlyDictionary<long, StudentIdentity> studentsByCanvasId,
        IReadOnlyDictionary<string, StudentIdentity> studentsByCode,
        ISet<string> unmatchedStudentIds)
    {
        if (studentsByCanvasId.TryGetValue(canvasUserId, out var canvasStudent))
        {
            return canvasStudent;
        }

        sisUserId = StudentIdentityNormalizer.SchoolUsername(sisUserId);
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
        enrollment.Grades?.CurrentPoints is not null ||
        enrollment.Grades?.CurrentGrade is not null ||
        enrollment.Grades?.FinalGrade is not null ||
        enrollment.Grades?.UnpostedCurrentScore is not null ||
        enrollment.Grades?.UnpostedFinalScore is not null ||
        enrollment.Grades?.UnpostedCurrentPoints is not null ||
        enrollment.Grades?.UnpostedCurrentGrade is not null ||
        enrollment.Grades?.UnpostedFinalGrade is not null ||
        enrollment.OverrideScore is not null ||
        enrollment.Grades?.OverrideScore is not null;

    private static bool GradingPeriodOverlapsCourseTerm(
        CanvasGradingPeriodDto period, CanvasCourseDto course)
    {
        if (course.Term?.GradingPeriodGroupId is { } groupId &&
            period.GradingPeriodGroupId is { } periodGroupId)
        {
            return periodGroupId == groupId;
        }

        var courseStart = course.StartAt ?? course.Term?.StartAt;
        var courseEnd = course.EndAt ?? course.Term?.EndAt;
        return (courseStart is null || period.EndDate is null || period.EndDate >= courseStart) &&
            (courseEnd is null || period.StartDate is null || period.StartDate <= courseEnd);
    }

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
        public int Sections { get; set; }
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
            Sections += other.Sections;
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
        List<CanvasCourseSectionDto> Sections,
        List<CanvasAssignmentDto> Assignments,
        List<CanvasAssignmentGroupDto> Groups,
        List<CanvasEnrollmentDto> Enrollments,
        List<GradingPeriodEnrollments> GradingPeriodEnrollments,
        List<CanvasSubmissionDto> Submissions,
        bool HasActiveStudents);

    private sealed record CanvasGrade(string GradeLevel, long AccountId);

    private sealed record CanvasGradeCounts(int StudentsWithGrade, int StudentsWithMultipleGrades);

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
    int Accounts,
    int Courses,
    int Sections,
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
