using System.Text.Json.Serialization;

namespace OlympiaStudent360.CanvasEtl;

internal sealed record CanvasTermDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CanvasGradingPeriodDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("start_date")] DateTime? StartDate,
    [property: JsonPropertyName("end_date")] DateTime? EndDate,
    [property: JsonPropertyName("weight")] double? Weight);

internal sealed record CanvasGradingPeriodsDto(
    [property: JsonPropertyName("grading_periods")] List<CanvasGradingPeriodDto> GradingPeriods);

internal sealed record CanvasCourseDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("sis_course_id")] string? SisCourseId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("course_code")] string? CourseCode,
    [property: JsonPropertyName("workflow_state")] string? WorkflowState,
    [property: JsonPropertyName("account_id")] long? AccountId,
    [property: JsonPropertyName("root_account_id")] long? RootAccountId,
    [property: JsonPropertyName("enrollment_term_id")] long? EnrollmentTermId,
    [property: JsonPropertyName("start_at")] DateTime? StartAt,
    [property: JsonPropertyName("end_at")] DateTime? EndAt,
    [property: JsonPropertyName("term")] CanvasTermDto? Term);

internal sealed record CanvasAccountDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("parent_account_id")] long? ParentAccountId,
    [property: JsonPropertyName("root_account_id")] long? RootAccountId,
    [property: JsonPropertyName("sis_account_id")] string? SisAccountId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("workflow_state")] string? WorkflowState);

internal sealed record CanvasCourseSectionDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("course_id")] long CourseId,
    [property: JsonPropertyName("sis_section_id")] string? SisSectionId,
    [property: JsonPropertyName("sis_course_id")] string? SisCourseId,
    [property: JsonPropertyName("integration_id")] string? IntegrationId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("start_at")] DateTime? StartAt,
    [property: JsonPropertyName("end_at")] DateTime? EndAt,
    [property: JsonPropertyName("total_students")] int? TotalStudents,
    [property: JsonPropertyName("restrict_enrollments_to_section_dates")] bool? RestrictEnrollmentsToSectionDates);

internal sealed record CanvasAssignmentDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("points_possible")] double? PointsPossible,
    [property: JsonPropertyName("due_at")] DateTime? DueAt,
    [property: JsonPropertyName("workflow_state")] string? WorkflowState,
    [property: JsonPropertyName("assignment_group_id")] long? AssignmentGroupId,
    [property: JsonPropertyName("grading_type")] string? GradingType,
    [property: JsonPropertyName("unlock_at")] DateTime? UnlockAt,
    [property: JsonPropertyName("lock_at")] DateTime? LockAt,
    [property: JsonPropertyName("published")] bool Published,
    [property: JsonPropertyName("omit_from_final_grade")] bool OmitFromFinalGrade,
    [property: JsonPropertyName("position")] int? Position);

internal sealed record CanvasAssignmentGroupDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CanvasUserDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("sis_user_id")] string? SisUserId,
    [property: JsonPropertyName("integration_id")] string? IntegrationId,
    [property: JsonPropertyName("login_id")] string? LoginId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("short_name")] string? ShortName,
    [property: JsonPropertyName("sortable_name")] string? SortableName,
    [property: JsonPropertyName("email")] string? Email);

internal sealed record CanvasGradesDto(
    [property: JsonPropertyName("current_score")] double? CurrentScore,
    [property: JsonPropertyName("final_score")] double? FinalScore,
    [property: JsonPropertyName("current_points")] double? CurrentPoints,
    [property: JsonPropertyName("current_grade")] string? CurrentGrade,
    [property: JsonPropertyName("final_grade")] string? FinalGrade,
    [property: JsonPropertyName("unposted_current_score")] double? UnpostedCurrentScore,
    [property: JsonPropertyName("unposted_final_score")] double? UnpostedFinalScore,
    [property: JsonPropertyName("unposted_current_points")] double? UnpostedCurrentPoints,
    [property: JsonPropertyName("unposted_current_grade")] string? UnpostedCurrentGrade,
    [property: JsonPropertyName("unposted_final_grade")] string? UnpostedFinalGrade,
    [property: JsonPropertyName("override_score")] double? OverrideScore);

internal sealed record CanvasEnrollmentDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("course_id")] long CourseId,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("role_id")] long? RoleId,
    [property: JsonPropertyName("enrollment_state")] string? EnrollmentState,
    [property: JsonPropertyName("sis_user_id")] string? SisUserId,
    [property: JsonPropertyName("course_section_id")] long? CourseSectionId,
    [property: JsonPropertyName("sis_section_id")] string? SisSectionId,
    [property: JsonPropertyName("sis_course_id")] string? SisCourseId,
    [property: JsonPropertyName("sis_account_id")] string? SisAccountId,
    [property: JsonPropertyName("created_at")] DateTime? CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime? UpdatedAt,
    [property: JsonPropertyName("start_at")] DateTime? StartAt,
    [property: JsonPropertyName("end_at")] DateTime? EndAt,
    [property: JsonPropertyName("last_activity_at")] DateTime? LastActivityAt,
    [property: JsonPropertyName("last_attended_at")] DateTime? LastAttendedAt,
    [property: JsonPropertyName("total_activity_time")] long? TotalActivityTime,
    [property: JsonPropertyName("limit_privileges_to_course_section")] bool LimitPrivilegesToCourseSection,
    [property: JsonPropertyName("user")] CanvasUserDto? User,
    [property: JsonPropertyName("grades")] CanvasGradesDto? Grades,
    [property: JsonPropertyName("override_score")] double? OverrideScore);

internal sealed record CanvasSubmissionDto(
    [property: JsonPropertyName("id")] long? Id,
    [property: JsonPropertyName("assignment_id")] long AssignmentId,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("score")] double? Score,
    [property: JsonPropertyName("grade")] string? Grade,
    [property: JsonPropertyName("workflow_state")] string? WorkflowState,
    [property: JsonPropertyName("submission_type")] string? SubmissionType,
    [property: JsonPropertyName("submitted_at")] DateTime? SubmittedAt,
    [property: JsonPropertyName("graded_at")] DateTime? GradedAt,
    [property: JsonPropertyName("late")] bool Late,
    [property: JsonPropertyName("missing")] bool Missing,
    [property: JsonPropertyName("excused")] bool? Excused,
    [property: JsonPropertyName("seconds_late")] long? SecondsLate,
    [property: JsonPropertyName("attempt")] int? Attempt,
    [property: JsonPropertyName("posted_at")] DateTime? PostedAt,
    [property: JsonPropertyName("grader_id")] long? GraderId,
    [property: JsonPropertyName("points_deducted")] double? PointsDeducted,
    [property: JsonPropertyName("late_policy_status")] string? LatePolicyStatus,
    [property: JsonPropertyName("cached_due_date")] DateTime? CachedDueDate,
    [property: JsonPropertyName("user")] CanvasUserDto? User);
