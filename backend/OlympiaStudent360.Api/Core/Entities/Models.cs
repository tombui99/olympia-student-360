using Microsoft.AspNetCore.Identity;

namespace OlympiaStudent360.Api.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<TeacherAccessGrant> AccessGrants { get; set; } = [];
    public ICollection<TeacherClassAccess> ClassAccesses { get; set; } = [];
    public ICollection<TeacherSchoolLevelAccess> SchoolLevelAccesses { get; set; } = [];
}

/// <summary>A data-domain permission assigned to a teacher account.</summary>
public class TeacherAccessGrant : BaseEntity
{
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
    public string Scope { get; set; } = default!;
}

/// <summary>A homeroom a class-scoped teacher is allowed to access.</summary>
public class TeacherClassAccess : BaseEntity
{
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
    public string Homeroom { get; set; } = default!;
}

/// <summary>A school-level group whose grades a teacher is allowed to access.</summary>
public class TeacherSchoolLevelAccess : BaseEntity
{
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
    public string SchoolLevel { get; set; } = default!;
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class StudentProfile : BaseEntity
{
    public long? CanvasUserId { get; set; }
    /// <summary>Permanent, authoritative student code from SIS (for example OS012223-18).</summary>
    public string? SISStudentCode { get; set; }
    /// <summary>Normalized school-email username used to join Canvas and SIS identities.</summary>
    public string SISUserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? ForeignName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    /// <summary>Current fallback grade derived from structured Canvas grade-account membership.</summary>
    public string? CanvasGradeLevel { get; set; }
    /// <summary>SIS-owned grade. This takes precedence over CanvasGradeLevel in reporting.</summary>
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? HomeroomCode { get; set; }
    public string? SISStatus { get; set; }
    public DateTime? SISLastImportedAtUtc { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public ICollection<StudentBehaviour> Behaviours { get; set; } = [];
    public ICollection<StudentProject> Projects { get; set; } = [];
    public ICollection<SISStudentParent> Parents { get; set; } = [];
    public ICollection<StudentMentalHealthRecord> MentalHealthRecords { get; set; } = [];
    public ICollection<StudentHealthVisit> HealthVisits { get; set; } = [];
    public ICollection<StudentBusAttendanceRecord> BusAttendanceRecords { get; set; } = [];
    public ICollection<StudentSupportAction> SupportActions { get; set; } = [];
    public ICollection<StudentCareerGuidanceRecord> CareerGuidanceRecords { get; set; } = [];
}

/// <summary>
/// A staff-authored intervention or enrichment action in a student's support and development
/// journey. The record captures what the school tried, why it was tried, and the observed result
/// without exposing the underlying behaviour, grade, or wellbeing source record.
/// </summary>
public class StudentSupportAction : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime ActionDate { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string? Term { get; set; }
    public string Category { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Goal { get; set; } = default!;
    public string ActionTaken { get; set; } = default!;
    public string? Result { get; set; }
    public string? NextStep { get; set; }
    public string Status { get; set; } = default!;
    public string Progress { get; set; } = default!;
    public string? Subject { get; set; }
    public bool ShareWithFamily { get; set; } = true;
    public string CreatedByUserId { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
}

/// <summary>
/// A staff-authored career and university guidance record for a student. One model covers the
/// recurring reports used by the university and career counselling team: general plans,
/// university applications, preparation for younger cohorts, internships, and counselling.
/// Record-type-specific fields remain nullable so each workflow only asks teachers for relevant
/// information.
/// </summary>
public class StudentCareerGuidanceRecord : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string RecordType { get; set; } = default!;
    public DateTime RecordDate { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string? Term { get; set; }
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;

    public string? TargetCountries { get; set; }
    public string? TargetUniversities { get; set; }
    public string? TargetMajors { get; set; }
    public string? CareerInterests { get; set; }
    public int? ApplicationProgressPercent { get; set; }
    public int? PreparationProgressPercent { get; set; }
    public int? QualityRating { get; set; }

    public string? ActivitiesSummary { get; set; }
    public string? Issues { get; set; }
    public string? Solutions { get; set; }
    public string? StudentSupport { get; set; }
    public string? StudentFeedback { get; set; }
    public string? NextSteps { get; set; }
    public string? Recommendations { get; set; }
    public string? Proposals { get; set; }

    public string? InternshipCompany { get; set; }
    public string? InternshipRole { get; set; }
    public DateTime? InternshipStartDate { get; set; }
    public DateTime? InternshipEndDate { get; set; }
    public int? InternshipProgressPercent { get; set; }
    public int? InternshipQualityRating { get; set; }
    public string? StudentSelfAssessment { get; set; }
    public string? HostAssessment { get; set; }
    public string? FinalProduct { get; set; }

    public string RecordedByUserId { get; set; } = default!;
    public string RecordedByName { get; set; } = default!;
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
}

/// <summary>
/// Singleton, school-wide policy containing direct, human-readable classification rules.
/// </summary>
public class StudentClassificationSetting : BaseEntity
{
    public int RiskWindowDays { get; set; } = 7;
    public int HighRiskBehaviourCount { get; set; } = 5;
    public int HighRiskBusViolationCount { get; set; } = 5;
    public decimal HighRiskCanvasAverageBelow { get; set; } = 50;
    public int CommendationWindowDays { get; set; } = 7;
    public int CommendationPositiveBehaviourCount { get; set; } = 3;
    public decimal CommendationCanvasAverageAtLeast { get; set; } = 85;
    public int CommendationMaxBusViolations { get; set; } = 0;
    public string? UpdatedByUserId { get; set; }
}

/// <summary>
/// Student mental health / wellbeing (tâm lý) record, captured through the manual import
/// module by counsellors.
/// </summary>
public class StudentMentalHealthRecord : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime RecordDate { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string ConcernType { get; set; } = default!;
    public string RiskLevel { get; set; } = default!;
    public string? Notes { get; set; }
    public string RecordedByUserId { get; set; } = default!;
    public string RecordedByName { get; set; } = default!;
    /// <summary>Values for admin-defined custom fields, keyed by field key. JSON object.</summary>
    public string? CustomFieldsJson { get; set; }
}

/// <summary>
/// A health/injury visit recorded by school staff through manual entry or Excel import.
/// Legacy identity snapshot columns are retained so existing visit rows can be migrated
/// without data loss.
/// </summary>
public class StudentHealthVisit : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public StudentProfile? Student { get; set; }
    /// <summary>Thời gian khám.</summary>
    public DateTime ExamTime { get; set; }
    /// <summary>Họ và tên (as recorded in SIS).</summary>
    public string FullName { get; set; } = default!;
    /// <summary>Giới tính.</summary>
    public string? Gender { get; set; }
    /// <summary>Lớp.</summary>
    public string? ClassName { get; set; }
    /// <summary>Triệu chứng.</summary>
    public string? Symptoms { get; set; }
    /// <summary>Chuẩn đoán.</summary>
    public string? Diagnosis { get; set; }
    /// <summary>Phân loại.</summary>
    public string? Classification { get; set; }
    /// <summary>Xử trí.</summary>
    public string? Treatment { get; set; }
    /// <summary>Thuốc sử dụng.</summary>
    public string? Medication { get; set; }
    /// <summary>Ghi chú bổ sung của phòng y tế.</summary>
    public string? Notes { get; set; }
    /// <summary>Đã gửi thông báo cho người liên quan.</summary>
    public bool NotificationSent { get; set; }
    /// <summary>Y, BS khám bệnh.</summary>
    public string? ExaminedBy { get; set; }
    /// <summary>Values for admin-defined custom fields, keyed by field key. JSON object.</summary>
    public string? CustomFieldsJson { get; set; }
}

/// <summary>
/// A manually imported bus check-in exception. SIS remains the source of truth for buses,
/// routes and normal ridership; this table records the behaviour exceptions the school
/// follows up on: late pickup/drop-off and failure to wear a seatbelt.
/// </summary>
public class StudentBusAttendanceRecord : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime AttendanceDate { get; set; }
    public string TripDirection { get; set; } = default!;
    public string ViolationType { get; set; } = default!;
    public string? BusCode { get; set; }
    public string? RouteName { get; set; }
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? CustomFieldsJson { get; set; }
}

/// <summary>Parent demographic data synchronised from SIS.</summary>
public class SISStudentParent : BaseEntity
{
    public int StudentProfileId { get; set; }
    public string SISUserId { get; set; } = default!;
    public StudentProfile Student { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Relationship { get; set; }
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int? BirthYear { get; set; }
    public string? AgeGroup { get; set; }
    public string? Occupation { get; set; }
    public string? EmploymentSector { get; set; }
    public StudentParentNote? ContactNote { get; set; }
}

/// <summary>
/// Student 360-owned context recorded by school staff before contacting an SIS parent.
/// Kept separate from SISStudentParent so SIS remains the source of truth for contact data.
/// </summary>
public class StudentParentNote : BaseEntity
{
    public int SISStudentParentId { get; set; }
    public SISStudentParent Parent { get; set; } = default!;
    public string Notes { get; set; } = default!;
    public string UpdatedByUserId { get; set; } = default!;
    public string UpdatedByName { get; set; } = default!;
}

/// <summary>Daily, student-grained report measures built outside HTTP requests.</summary>
public class ImportedReportAggregate : BaseEntity
{
    public string Source { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string StudentKey { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? SISUserId { get; set; }
    public string? Grade { get; set; }
    public string? Homeroom { get; set; }
    public string? CanvasGrade { get; set; }
    public string? ClassName { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public DateTime EventDay { get; set; }
    public DateTime LatestEvent { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? Diagnosis { get; set; }
    public string? Symptoms { get; set; }
    public string? EnteredBy { get; set; }
    public int RecordCount { get; set; }
    public int AttentionCount { get; set; }
    public int Recent30Count { get; set; }
}

/// <summary>One change marker per source write; the background builder drains these after a successful swap.</summary>
public class ImportedReportChange : BaseEntity { }

public class ImportedReportBuild : BaseEntity
{
    public int LastChangeId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}

/// <summary>Daily-refreshed, period-specific risk and count measures for a student.</summary>
public class StudentRiskSnapshot : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string AcademicYear { get; set; } = default!;
    public string Term { get; set; } = default!;
    public DateTime AsOfUtc { get; set; }
    public decimal? AverageCanvasScore { get; set; }
    public int BehaviourCount { get; set; }
    public int PositiveBehaviourCount { get; set; }
    public int ConcerningBehaviourCount { get; set; }
    public int ProjectCount { get; set; }
    public int MentalHealthCount { get; set; }
    public int RiskConcerningCount { get; set; }
    public int RiskBusCount { get; set; }
    public string RiskLevelWithBus { get; set; } = default!;
    public string RiskLevelWithoutBus { get; set; } = default!;
    public string RiskFlagsWithBusJson { get; set; } = default!;
    public string RiskFlagsWithoutBusJson { get; set; } = default!;
    public int RiskFlagCountWithBus { get; set; }
    public int RiskFlagCountWithoutBus { get; set; }
}

public class StudentBehaviour : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = default!;
    public string? ClassificationCategory { get; set; }
    public string? Notes { get; set; }
    public string? ReporterName { get; set; }
    public string? RecordedByEmail { get; set; }
    public string? RecordedByTitle { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public string? IncidentTimeText { get; set; }
    public string? Decision { get; set; }
    public string? ConductDeductionLevel { get; set; }
    public int? OccurrenceCount { get; set; }
    public string? BehaviourPointsDeducted { get; set; }
    public int? SourceRowNumber { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceStudentName { get; set; }
    /// <summary>Values for admin-defined custom fields, keyed by field key. JSON object.</summary>
    public string? CustomFieldsJson { get; set; }
}

public class StudentProject : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string ProjectName { get; set; } = default!;
    public string? Subject { get; set; }
    public string Status { get; set; } = default!;
    public string? TeacherNotes { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public DateTime? ActivityDate { get; set; }
    /// <summary>Values for admin-defined custom fields, keyed by field key. JSON object.</summary>
    public string? CustomFieldsJson { get; set; }
}

public class AcademicPeriod : BaseEntity
{
    public string AcademicYear { get; set; } = default!;
    public string Term { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class DashboardMetric : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Unit { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<DashboardMetricObservation> Observations { get; set; } = [];
}

public class DashboardMetricObservation : BaseEntity
{
    public int DashboardMetricId { get; set; }
    public DashboardMetric Metric { get; set; } = default!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Value { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? KpiTarget { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
}

public class DashboardPreference : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
}

// ---------------------------------------------------------------------------
// Canvas LMS warehouse tables.
//
// These are source-preserving warehouse records populated from the Canvas REST API.
// Canvas-owned users, accounts, courses and sections remain separate from the future
// SIS-owned student/class dimensions. Canvas ids are retained for idempotent upserts.
// ---------------------------------------------------------------------------

public class CanvasAccount : BaseEntity
{
    public long CanvasAccountId { get; set; }
    public long? ParentAccountId { get; set; }
    public long? RootAccountId { get; set; }
    public string? SisAccountId { get; set; }
    public string? Name { get; set; }
    public string? WorkflowState { get; set; }
}

public class CanvasUser : BaseEntity
{
    public long CanvasUserId { get; set; }
    public string? SisUserId { get; set; }
    public string? IntegrationId { get; set; }
    public string? LoginId { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? SortableName { get; set; }
    public string? Email { get; set; }
}

public class CanvasCourse : BaseEntity
{
    /// <summary>Canvas courses.id.</summary>
    public long CanvasCourseId { get; set; }
    public string? SisCourseId { get; set; }
    public string? Name { get; set; }
    public string? CourseCode { get; set; }
    public string? WorkflowState { get; set; }
    public long? CanvasAccountId { get; set; }
    public long? CanvasRootAccountId { get; set; }
    /// <summary>Grade derived only from a structured Canvas grade account ancestor.</summary>
    public string? CanvasGradeLevel { get; set; }
    public long? CanvasGradeSourceAccountId { get; set; }
    public long? EnrollmentTermId { get; set; }
    public string? EnrollmentTermName { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public ICollection<CanvasAssignment> Assignments { get; set; } = [];
    public ICollection<CanvasCourseSection> Sections { get; set; } = [];
    public ICollection<CanvasEnrollment> Enrollments { get; set; } = [];
}

/// <summary>A Canvas course section/class. Future SIS classes will use a separate SIS-owned table.</summary>
public class CanvasCourseSection : BaseEntity
{
    public long CanvasSectionId { get; set; }
    public long CanvasCourseId { get; set; }
    public CanvasCourse? Course { get; set; }
    public string? SisSectionId { get; set; }
    public string? SisCourseId { get; set; }
    public string? IntegrationId { get; set; }
    public string? Name { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int? TotalStudents { get; set; }
    public bool? RestrictEnrollmentsToSectionDates { get; set; }
}

public class CanvasAssignment : BaseEntity
{
    /// <summary>Canvas assignments.id.</summary>
    public long CanvasAssignmentId { get; set; }
    public long CanvasCourseId { get; set; }
    public CanvasCourse? Course { get; set; }
    public string? Name { get; set; }
    public double? PointsPossible { get; set; }
    public DateTime? DueAt { get; set; }
    public string? WorkflowState { get; set; }
    public long? AssignmentGroupId { get; set; }
    public string? AssignmentGroupName { get; set; }
    public string? GradingType { get; set; }
    public DateTime? UnlockAt { get; set; }
    public DateTime? LockAt { get; set; }
    public bool Published { get; set; }
    public bool OmitFromFinalGrade { get; set; }
    public int? Position { get; set; }
    public ICollection<CanvasSubmission> Submissions { get; set; } = [];
}

public class CanvasEnrollment : BaseEntity
{
    /// <summary>Canvas enrollments.id.</summary>
    public long CanvasEnrollmentId { get; set; }
    public long CanvasUserId { get; set; }
    public long CanvasCourseId { get; set; }
    public CanvasCourse? Course { get; set; }
    /// <summary>Canvas enrollment type, e.g. StudentEnrollment, TeacherEnrollment.</summary>
    public string? Type { get; set; }
    public string? Role { get; set; }
    public long? RoleId { get; set; }
    public string? EnrollmentState { get; set; }
    public string? SisUserId { get; set; }
    public long? CanvasCourseSectionId { get; set; }
    public CanvasCourseSection? Section { get; set; }
    public string? SisSectionId { get; set; }
    public string? SisCourseId { get; set; }
    public string? SisAccountId { get; set; }
    public DateTime? SourceCreatedAt { get; set; }
    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? LastAttendedAt { get; set; }
    public long? TotalActivityTimeSeconds { get; set; }
    public bool LimitPrivilegesToCourseSection { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public ICollection<CanvasCourseScore> Scores { get; set; } = [];
}

public class CanvasCourseScore : BaseEntity
{
    /// <summary>Warehouse key; Canvas exposes these values inside an enrollment, not as a score resource ID.</summary>
    public long WarehouseScoreFactId { get; set; }
    public long CanvasEnrollmentId { get; set; }
    public CanvasEnrollment? Enrollment { get; set; }
    /// <summary>Warehouse relationship to the SIS-owned student profile.</summary>
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public long? GradingPeriodId { get; set; }
    public string? GradingPeriodTitle { get; set; }
    public DateTime? GradingPeriodStartDate { get; set; }
    public DateTime? GradingPeriodEndDate { get; set; }
    public double? GradingPeriodWeight { get; set; }
    public double? CurrentScore { get; set; }
    public double? FinalScore { get; set; }
    public double? CurrentPoints { get; set; }
    public string? CurrentGrade { get; set; }
    public string? FinalGrade { get; set; }
    public double? UnpostedCurrentScore { get; set; }
    public double? UnpostedFinalScore { get; set; }
    public double? UnpostedCurrentPoints { get; set; }
    public string? UnpostedCurrentGrade { get; set; }
    public string? UnpostedFinalGrade { get; set; }
    public double? EnrollmentOverrideScore { get; set; }
    public double? GradesOverrideScore { get; set; }
}

public class CanvasSubmission : BaseEntity
{
    /// <summary>Canvas submissions.id.</summary>
    public long CanvasSubmissionId { get; set; }
    public long CanvasAssignmentId { get; set; }
    public CanvasAssignment? Assignment { get; set; }
    public long CanvasCourseId { get; set; }
    public long CanvasUserId { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public double? Score { get; set; }
    public string? Grade { get; set; }
    /// <summary>Canvas submissions.workflow_state, e.g. submitted, graded, unsubmitted, pending_review.</summary>
    public string? WorkflowState { get; set; }
    public string? SubmissionType { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    /// <summary>Canvas submissions.cached_due_date exactly as returned.</summary>
    public DateTime? CachedDueDate { get; set; }
    public bool Late { get; set; }
    public bool Missing { get; set; }
    public bool? Excused { get; set; }
    public long? SecondsLate { get; set; }
    public int? Attempt { get; set; }
    public DateTime? PostedAt { get; set; }
    public long? GraderId { get; set; }
    public double? PointsDeducted { get; set; }
    public string? LatePolicyStatus { get; set; }
}

/// <summary>
/// A monthly, student-and-course-grained Canvas dashboard fact. The Canvas ETL rebuilds these
/// rows after it commits raw submissions and scores, so dashboard requests do not have to scan
/// the warehouse fact tables. ValueSum/SampleCount preserve weighted averages after filtering.
/// </summary>
public class CanvasDashboardMetricAggregate : BaseEntity
{
    public string MetricKey { get; set; } = default!;
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public long CanvasCourseId { get; set; }
    public CanvasCourse Course { get; set; } = default!;
    /// <summary>
    /// Assignment-grain lineage for submission metrics. Keeping this dimension in the persisted
    /// aggregate lets configurable reports select real Canvas activities without reading the raw
    /// submissions table. Course-score aggregates intentionally leave it null.
    /// </summary>
    public long? CanvasAssignmentId { get; set; }
    public CanvasAssignment? Assignment { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string Term { get; set; } = "";
    /// <summary>Natural Canvas event month, used when no academic year is selected.</summary>
    public DateTime? PeriodStart { get; set; }
    /// <summary>Event month adjusted to the row's reporting year, used by year-scoped reports.</summary>
    public DateTime? AcademicPeriodStart { get; set; }
    /// <summary>Exact reporting day behind AcademicPeriodStart, retained for weekly activity filters.</summary>
    public DateTime? AcademicActivityDate { get; set; }
    public double ValueSum { get; set; }
    /// <summary>Denominator for average metrics; zero for additive count metrics.</summary>
    public int SampleCount { get; set; }
}

/// <summary>
/// Report-ready Canvas totals at student/course/assignment-group/month grain. The ETL rebuilds
/// these with the dashboard facts so leadership reports can filter and summarize without pivoting
/// assignment-level metric rows for every request. ScoreSum/ScoreCount retain the existing
/// assignment-percentage average; EarnedPoints/PossiblePoints retain the Canvas group ratio.
/// Final course scores use separate rows and never inherit an assignment-group classification.
/// </summary>
public class CanvasLeadershipReportAggregate : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public long CanvasCourseId { get; set; }
    public CanvasCourse Course { get; set; } = default!;
    public string AcademicYear { get; set; } = default!;
    public string Term { get; set; } = "";
    public DateTime? AcademicPeriodStart { get; set; }
    /// <summary>Activity day for assignment results; semester course scores leave it null.</summary>
    public DateTime? AcademicActivityDate { get; set; }
    public bool IsFinalCourseScore { get; set; }
    public string? AssignmentGroupName { get; set; }
    public string? AssessmentCategory { get; set; }
    public int Submitted { get; set; }
    public int Missing { get; set; }
    public int Late { get; set; }
    public int Graded { get; set; }
    public double ScoreSum { get; set; }
    public int ScoreCount { get; set; }
    /// <summary>Eligible graded Canvas submission points, summed at this student/course/group/period grain.</summary>
    public double EarnedPoints { get; set; }
    public double PossiblePoints { get; set; }
}

/// <summary>Identifies the newest completed Canvas import included in the report snapshot.</summary>
public class CanvasDashboardAggregateBuild : BaseEntity
{
    public int SchemaVersion { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public int LastCompletedSyncRunId { get; set; }
    public string? PeriodConfigurationHash { get; set; }
}

/// <summary>Durable invalidation saved with course facts and drained only when reports commit.</summary>
public class CanvasReportCourseChange
{
    // No course FK: a deleted course must also invalidate its old report rows.
    public long CanvasCourseId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

/// <summary>Durable high-water mark and last-run status for one Canvas account.</summary>
public class CanvasSyncState : BaseEntity
{
    public string SourceKey { get; set; } = default!;
    public DateTime? LastSuccessfulWatermarkUtc { get; set; }
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastCompletedAtUtc { get; set; }
    public string? LastRunMode { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
}

/// <summary>An append-only audit record for one Canvas ETL execution.</summary>
public class CanvasSyncRun : BaseEntity
{
    public bool ReportChangesTracked { get; set; }
    public string SourceKey { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Workflow { get; set; } = default!;
    public string Status { get; set; } = "running";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? WindowStartUtc { get; set; }
    public DateTime WatermarkUtc { get; set; }
    public string? Error { get; set; }
    public int? Courses { get; set; }
    public int? Assignments { get; set; }
    public int? Enrollments { get; set; }
    public int? Scores { get; set; }
    public int? Submissions { get; set; }
}

/// <summary>
/// An admin/school-administration-defined custom field on an import module. The field's values are
/// stored per record in <c>CustomFieldsJson</c> rather than as dedicated columns, so any
/// number of custom fields can be added without schema changes.
/// </summary>
public class ImportModuleField : BaseEntity
{
    /// <summary>Import module the field belongs to, e.g. "student-behaviours".</summary>
    public string ModuleId { get; set; } = default!;
    /// <summary>Stable slug used as the JSON key and Excel column header.</summary>
    public string FieldKey { get; set; } = default!;
    public string Label { get; set; } = default!;
    /// <summary>text | textarea | number | date | select | checkbox.</summary>
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
    /// <summary>JSON array of option strings, for the "select" field type.</summary>
    public string? OptionsJson { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A configurable option for a built-in dropdown on a student record module.</summary>
public class StudentRecordFieldOption : BaseEntity
{
    public string ModuleId { get; set; } = default!;
    public string FieldKey { get; set; } = default!;
    public string Value { get; set; } = default!;
    /// <summary>Optional semantic group, currently positive or concerning for behaviour types.</summary>
    public string? OptionGroup { get; set; }
    public int SortOrder { get; set; }
}

public class ImportHistory : BaseEntity
{
    public string ModuleId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string ImportedByUserId { get; set; } = default!;
    public string ImportedByUsername { get; set; } = default!;
    public string ImportedByName { get; set; } = default!;
    public ICollection<StudentBehaviour> Behaviours { get; set; } = [];
    public ICollection<StudentProject> Projects { get; set; } = [];
    public ICollection<StudentMentalHealthRecord> MentalHealthRecords { get; set; } = [];
    public ICollection<StudentBusAttendanceRecord> BusAttendanceRecords { get; set; } = [];
    public ICollection<StudentHealthVisit> HealthVisits { get; set; } = [];
}

/// <summary>A student-linked form authored by school administrators and shared publicly.</summary>
public class CustomForm : BaseEntity
{
    public string PublicId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool IsArchived { get; set; }
    public bool AllowMultipleSubmissions { get; set; } = true;
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int? ResponseLimit { get; set; }
    public string DefinitionJson { get; set; } = default!;
    public string CreatedByUserId { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;
    public ICollection<CustomFormResponse> Responses { get; set; } = [];
    public ICollection<CustomFormAccessSession> AccessSessions { get; set; } = [];
}

/// <summary>A submitted response. QuestionSnapshotJson keeps history readable after form edits.</summary>
public class CustomFormResponse : BaseEntity
{
    public int CustomFormId { get; set; }
    public CustomForm Form { get; set; } = default!;
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string AnswersJson { get; set; } = default!;
    public string QuestionSnapshotJson { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

/// <summary>Short-lived capability issued only after a guardian/student identity match.</summary>
public class CustomFormAccessSession : BaseEntity
{
    public int CustomFormId { get; set; }
    public CustomForm Form { get; set; } = default!;
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public class CustomReport : BaseEntity
{
    public string OwnerUserId { get; set; } = default!;
    public string OwnerUsername { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string DefinitionJson { get; set; } = default!;

    public string SharedWithJson { get; set; } = "[]";
    public ICollection<WorkspacePreference> Preferences { get; set; } = [];
}

public class WorkspacePreference : BaseEntity
{
    public int CustomReportId { get; set; }
    public CustomReport CustomReport { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public string ChartOrderJson { get; set; } = "[]";
}

/// <summary>One ERP report row. Reports can overlap, so the report name is part of its identity.</summary>
public class ErpMealStatus : BaseEntity
{
    public string Feed { get; set; } = default!;
    public string SourceRecordKey { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string ErpStudentId { get; set; } = default!;
    public string? StudentName { get; set; }
    public string? Email { get; set; }
    public string? ClassCode { get; set; }
    public DateTime MealDate { get; set; }
    public string Meal { get; set; } = default!;
    public string? OrderStatus { get; set; }
    public string? TicketStatus { get; set; }
    public string MatchStatus { get; set; } = default!;
    public DateTime ImportedAtUtc { get; set; }
}

/// <summary>One ERP canteen export line, including repeated purchases of the same product.</summary>
public class ErpCanteenPurchase : BaseEntity
{
    public string SourceRecordKey { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string ErpStudentId { get; set; } = default!;
    public string? StudentName { get; set; }
    public string? ClassCode { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public string MatchStatus { get; set; } = default!;
    public DateTime ImportedAtUtc { get; set; }
}

public class ErpMealSyncRun : BaseEntity
{
    public string Feed { get; set; } = default!;
    public string SourceDescription { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int ReceivedRows { get; set; }
    public int InsertedRows { get; set; }
    public int UpdatedRows { get; set; }
    public int UnmatchedRows { get; set; }
    public int ConflictRows { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Error { get; set; }
}

/// <summary>Persisted student/day/meal measures read by the meal reports.</summary>
public class ErpMealDailyAggregate : BaseEntity
{
    public string StudentKey { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string ErpStudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public DateTime EventDay { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime MonthStart { get; set; }
    /// <summary>Empty for canteen purchases, otherwise the ERP meal label.</summary>
    public string Meal { get; set; } = default!;
    public int OrderedNoTicketCount { get; set; }
    public int NoOrderCount { get; set; }
    public int CoffeeLineCount { get; set; }
    public int CoffeeUnitCount { get; set; }
}

/// <summary>One deduplicated meal event or one canteen item line; Coffee Shop details sum these by student/day.</summary>
public class ErpMealReportDetail : BaseEntity
{
    public string Report { get; set; } = default!;
    public string StudentKey { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string ErpStudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public DateTime EventDay { get; set; }
    public DateTime WeekStart { get; set; }
    public DateTime MonthStart { get; set; }
    public string? Meal { get; set; }
    public string? OrderStatus { get; set; }
    public string? TicketStatus { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
    public int SourceRowId { get; set; }
}

public class ErpMealReportBuild : BaseEntity
{
    public int LastSyncRunId { get; set; }
    public int LastStudentChangeId { get; set; }
    public DateTime CompletedAtUtc { get; set; }
}
