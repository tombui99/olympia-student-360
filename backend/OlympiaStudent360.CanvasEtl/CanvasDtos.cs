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
    [property: JsonPropertyName("enrollment_term_id")] long? EnrollmentTermId,
    [property: JsonPropertyName("start_at")] DateTime? StartAt,
    [property: JsonPropertyName("end_at")] DateTime? EndAt,
    [property: JsonPropertyName("term")] CanvasTermDto? Term);

internal sealed record CanvasAssignmentDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("points_possible")] double? PointsPossible,
    [property: JsonPropertyName("due_at")] DateTime? DueAt,
    [property: JsonPropertyName("workflow_state")] string? WorkflowState,
    [property: JsonPropertyName("assignment_group_id")] long? AssignmentGroupId,
    [property: JsonPropertyName("grading_type")] string? GradingType);

internal sealed record CanvasAssignmentGroupDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CanvasUserDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("sis_user_id")] string? SisUserId,
    [property: JsonPropertyName("integration_id")] string? IntegrationId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("short_name")] string? ShortName,
    [property: JsonPropertyName("sortable_name")] string? SortableName);

internal sealed record CanvasGradesDto(
    [property: JsonPropertyName("current_score")] double? CurrentScore,
    [property: JsonPropertyName("final_score")] double? FinalScore,
    [property: JsonPropertyName("override_score")] double? OverrideScore);

internal sealed record CanvasEnrollmentDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("course_id")] long CourseId,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("enrollment_state")] string? EnrollmentState,
    [property: JsonPropertyName("sis_user_id")] string? SisUserId,
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
    [property: JsonPropertyName("cached_due_date")] DateTime? CachedDueDate,
    [property: JsonPropertyName("user")] CanvasUserDto? User);
