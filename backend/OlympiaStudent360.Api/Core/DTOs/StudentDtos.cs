namespace OlympiaStudent360.Api.Core.DTOs;

public class StudentListItemDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public string? TeacherName { get; set; }
    public int BehaviourCount { get; set; }
    public int ProjectCount { get; set; }
    public int MentalHealthCount { get; set; }
    public string RiskLevel { get; set; } = "None";
    public int RiskFlagCount { get; set; }
}

public class StudentsPageDto
{
    public List<StudentListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<string> AvailableGrades { get; set; } = [];
}

public class RiskFlagDto
{
    public string Code { get; set; } = default!;
    public string Severity { get; set; } = default!;
    public int Value { get; set; }
}

public class AtRiskStudentDto : StudentListItemDto
{
    public List<RiskFlagDto> Flags { get; set; } = [];
}

public class AtRiskStudentsPageDto
{
    public List<AtRiskStudentDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int HighRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
}

public class StudentCommendationDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? TeacherName { get; set; }
    public DateTime IncidentDate { get; set; }
    public string CommendationType { get; set; } = default!;
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
}

public class StudentCommendationsPageDto
{
    public List<StudentCommendationDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int TotalCommendationCount { get; set; }
    public int StudentsRecognisedCount { get; set; }
    public int RecentCount { get; set; }
    public List<string> AvailableGrades { get; set; } = [];
}

public class CohortStatsDto
{
    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public int StudentCount { get; set; }
    public int AtRiskCount { get; set; }
    public int HighRiskCount { get; set; }
    public int TotalBehaviours { get; set; }
    public int TotalProjects { get; set; }
    public double AvgBehaviours { get; set; }
}

public class CohortsResponseDto
{
    public List<CohortStatsDto> Grades { get; set; } = [];
    public List<CohortStatsDto> Homerooms { get; set; } = [];
}

public class StudentProfileDetailDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public string? TeacherName { get; set; }
    public string RiskLevel { get; set; } = "None";
    public List<RiskFlagDto> Flags { get; set; } = [];
    public StudentSummaryDto Summary { get; set; } = new();
    public List<StudentBehaviourTrendPointDto> BehaviourTrend { get; set; } = [];
    public List<StudentBehaviourDto> Behaviours { get; set; } = [];
    public List<StudentProjectDto> Projects { get; set; } = [];
    public List<StudentParentDto> Parents { get; set; } = [];
    public List<StudentMentalHealthRecordDto> MentalHealthRecords { get; set; } = [];
    public List<StudentBusAttendanceRecordDto> BusAttendanceRecords { get; set; } = [];
    public List<SISHealthVisitDto> HealthVisits { get; set; } = [];
    public StudentCanvasDto Canvas { get; set; } = new();
}

public class StudentBehaviourTrendPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Total { get; set; }
}

public class StudentCanvasDto
{
    public bool HasData { get; set; }
    /// <summary>Academic year the headline tiles/courses reflect (the student's latest year).</summary>
    public string? AcademicYear { get; set; }
    public int CoursesEnrolled { get; set; }
    public decimal? AverageCourseGrade { get; set; }
    public decimal? AverageAssignmentScore { get; set; }
    public int AssignmentsSubmitted { get; set; }
    public int AssignmentsGraded { get; set; }
    public int MissingAssignments { get; set; }
    public int LateSubmissions { get; set; }
    public List<StudentCanvasCourseDto> Courses { get; set; } = [];
    public List<StudentCanvasSubmissionDto> RecentSubmissions { get; set; } = [];
    /// <summary>The student's grade trajectory across every academic year/term on record.</summary>
    public List<StudentCanvasJourneyPointDto> Journey { get; set; } = [];
}

public class StudentCanvasJourneyPointDto
{
    public string AcademicYear { get; set; } = default!;
    public string? Term { get; set; }
    /// <summary>The grade level the student was in that year, when it can be derived (e.g. "5").</summary>
    public string? GradeLevel { get; set; }
    public string Label { get; set; } = default!;
    public decimal? AverageGrade { get; set; }
    public int CourseCount { get; set; }
}

public class StudentCanvasCourseDto
{
    public string CourseName { get; set; } = default!;
    public string? Subject { get; set; }
    public string? Term { get; set; }
    public string? GradingPeriodTitle { get; set; }
    public decimal? CurrentScore { get; set; }
    public decimal? FinalScore { get; set; }
}

public class StudentCanvasSubmissionDto
{
    public string AssignmentTitle { get; set; } = default!;
    public string? CourseName { get; set; }
    public string? Subject { get; set; }
    public string WorkflowState { get; set; } = default!;
    public decimal? Score { get; set; }
    public decimal? PointsPossible { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool Late { get; set; }
    public bool Missing { get; set; }
    public bool Excused { get; set; }
}

public class StudentMentalHealthRecordDto
{
    public int Id { get; set; }
    public DateTime RecordDate { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string ConcernType { get; set; } = default!;
    public string RiskLevel { get; set; } = default!;
    public string? Notes { get; set; }
    public string RecordedByName { get; set; } = default!;
    public Dictionary<string, string> CustomFields { get; set; } = [];
}

/// <summary>Health/injury clinic visit synced from SIS (read-only).</summary>
public class SISHealthVisitDto
{
    public int Id { get; set; }
    public DateTime ExamTime { get; set; }
    public string FullName { get; set; } = default!;
    public string? Gender { get; set; }
    public string? ClassName { get; set; }
    public string? Symptoms { get; set; }
    public string? Diagnosis { get; set; }
    public string? Classification { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public string? ExaminedBy { get; set; }
}

public class StudentBusAttendanceRecordDto
{
    public int Id { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string TripDirection { get; set; } = default!;
    public string ViolationType { get; set; } = default!;
    public string? BusCode { get; set; }
    public string? RouteName { get; set; }
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = [];
}

public class StudentParentDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AgeGroup { get; set; }
    public string? Occupation { get; set; }
    public string? EmploymentSector { get; set; }
}

public class StudentSummaryDto
{
    public int TotalBehaviours { get; set; }
    public int BehavioursLast30Days { get; set; }
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
}

public class StudentBehaviourDto
{
    public int Id { get; set; }
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = default!;
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = [];
}

public class StudentProjectDto
{
    public int Id { get; set; }
    public string ProjectName { get; set; } = default!;
    public string? Subject { get; set; }
    public string Status { get; set; } = default!;
    public string? TeacherNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public DateTime? ActivityDate { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = [];
}
