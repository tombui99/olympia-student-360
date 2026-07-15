using Microsoft.AspNetCore.Identity;

namespace OlympiaStudent360.Api.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<StudentProfile> AssignedStudents { get; set; } = [];
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class StudentProfile : BaseEntity
{
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public string? TeacherUserId { get; set; }
    public ApplicationUser? Teacher { get; set; }
    public ICollection<StudentBehaviour> Behaviours { get; set; } = [];
    public ICollection<StudentProject> Projects { get; set; } = [];
    public ICollection<StudentParent> Parents { get; set; } = [];
    public ICollection<StudentMentalHealthRecord> MentalHealthRecords { get; set; } = [];
    public ICollection<SISHealthVisit> SISHealthVisits { get; set; } = [];
    public ICollection<StudentBusAttendanceRecord> BusAttendanceRecords { get; set; } = [];
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
/// A health/injury visit to the school clinic (phòng y tế). This is warehouse data that a
/// future ETL job will sync from SIS; the fields mirror the SIS medical log. Linked to a
/// StudentProfile where the SIS student can be resolved.
/// </summary>
public class SISHealthVisit : BaseEntity
{
    public int? StudentProfileId { get; set; }
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
    /// <summary>Y, BS khám bệnh.</summary>
    public string? ExaminedBy { get; set; }
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

public class StudentParent : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AgeGroup { get; set; }
    public string? Occupation { get; set; }
    public string? EmploymentSector { get; set; }
}

public class StudentBehaviour : BaseEntity
{
    public int? ImportHistoryId { get; set; }
    public ImportHistory? ImportHistory { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = default!;
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
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
    public string ChartOrdersJson { get; set; } = "{}";
}

// ---------------------------------------------------------------------------
// Canvas LMS warehouse tables.
//
// These mirror the parts of the Canvas Data 2 (DAP/CD2) schema we care about for
// student grades and assignments. They are the landing/warehouse tables that a
// future ETL job will populate from Canvas; for now they hold demo data seeded by
// DbInitializer. Canvas surrogate ids are kept (bigint) alongside a link to our
// own StudentProfile, resolved through the SIS id (StudentProfile.StudentCode ==
// Canvas pseudonyms.sis_user_id). Fact rows also carry the academic dimensions
// (AcademicYear/Term/Subject) so they participate in the dashboard filters exactly
// like the other imported sources.
// ---------------------------------------------------------------------------

public class CanvasCourse : BaseEntity
{
    /// <summary>Canvas courses.id.</summary>
    public long CanvasCourseId { get; set; }
    public string? SisSourceId { get; set; }
    public string Name { get; set; } = default!;
    public string? CourseCode { get; set; }
    public string WorkflowState { get; set; } = "available";
    public long? EnrollmentTermId { get; set; }
    public string? TermName { get; set; }
    /// <summary>Subject the course maps to, aligned with our Subject dimension.</summary>
    public string? Subject { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public ICollection<CanvasAssignment> Assignments { get; set; } = [];
    public ICollection<CanvasEnrollment> Enrollments { get; set; } = [];
}

public class CanvasAssignment : BaseEntity
{
    /// <summary>Canvas assignments.id.</summary>
    public long CanvasAssignmentId { get; set; }
    public long CanvasCourseId { get; set; }
    public CanvasCourse? Course { get; set; }
    public string Title { get; set; } = default!;
    public double? PointsPossible { get; set; }
    public DateTime? DueAt { get; set; }
    public string WorkflowState { get; set; } = "published";
    public string? AssignmentGroupName { get; set; }
    public string? GradingType { get; set; }
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
    public string Type { get; set; } = "StudentEnrollment";
    public string WorkflowState { get; set; } = "active";
    public string? SisUserId { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public ICollection<CanvasCourseScore> Scores { get; set; } = [];
}

public class CanvasCourseScore : BaseEntity
{
    /// <summary>Canvas scores.id.</summary>
    public long CanvasScoreId { get; set; }
    public long CanvasEnrollmentId { get; set; }
    public CanvasEnrollment? Enrollment { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    /// <summary>True for the overall course score row (as opposed to an assignment-group score).</summary>
    public bool CourseScore { get; set; } = true;
    public long? GradingPeriodId { get; set; }
    public string? GradingPeriodTitle { get; set; }
    public double? CurrentScore { get; set; }
    public double? FinalScore { get; set; }
    public double? OverrideScore { get; set; }
    public string? CourseName { get; set; }
    public string? Subject { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
}

public class CanvasSubmission : BaseEntity
{
    /// <summary>Canvas submissions.id.</summary>
    public long CanvasSubmissionId { get; set; }
    public long CanvasAssignmentId { get; set; }
    public CanvasAssignment? Assignment { get; set; }
    public long CanvasCourseId { get; set; }
    public long CanvasUserId { get; set; }
    public string? SisUserId { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public double? Score { get; set; }
    public string? Grade { get; set; }
    public double? PointsPossible { get; set; }
    /// <summary>Canvas submissions.workflow_state, e.g. submitted, graded, unsubmitted, pending_review.</summary>
    public string WorkflowState { get; set; } = "unsubmitted";
    public string? SubmissionType { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? GradedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public bool Late { get; set; }
    public bool Missing { get; set; }
    public bool Excused { get; set; }
    public long? SecondsLate { get; set; }
    public string? Subject { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
}

/// <summary>
/// An admin/principal-defined custom field on an import module. The field's values are
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
}

public class CustomReport : BaseEntity
{
    public string OwnerUserId { get; set; } = default!;
    public string OwnerUsername { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string DefinitionJson { get; set; } = default!;

    public string SharedWithJson { get; set; } = "[]";
}
