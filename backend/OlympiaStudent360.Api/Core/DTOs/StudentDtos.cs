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
    public int HealthIncidentCount { get; set; }
    public string RiskLevel { get; set; } = "None";
    public int RiskFlagCount { get; set; }
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
    public List<StudentBehaviourDto> Behaviours { get; set; } = [];
    public List<StudentProjectDto> Projects { get; set; } = [];
    public List<StudentParentDto> Parents { get; set; } = [];
    public List<StudentHealthIncidentDto> HealthIncidents { get; set; } = [];
}

public class StudentHealthIncidentDto
{
    public int Id { get; set; }
    public DateTime IncidentDate { get; set; }
    public string AcademicYear { get; set; } = default!;
    public string IncidentType { get; set; } = default!;
    public int SeverityLevel { get; set; }
    public string? BodyArea { get; set; }
    public string? Location { get; set; }
    public string Cause { get; set; } = default!;
    public string? ActionTaken { get; set; }
    public string RecordedByName { get; set; } = default!;
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
}
