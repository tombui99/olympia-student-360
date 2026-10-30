namespace OlympiaStudent360.Api.Core.DTOs;

public class TranscriptStudentDto
{
    public int Id { get; set; }
    public string SISUserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}

public class TranscriptStudentsPageDto
{
    public List<TranscriptStudentDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class SchoolTranscriptDto
{
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public string? StudentName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Gender { get; set; }
    public double? OverallAverage { get; set; }
    public double? OverallGpa { get; set; }
    public int TotalAcademicYears { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
    public List<TranscriptSubjectOptionDto> SubjectOptions { get; set; } = [];
    public List<SchoolTranscriptYearDto> Years { get; set; } = [];
}

public class SchoolTranscriptYearDto
{
    public string AcademicYear { get; set; } = default!;
    public double? AverageScore { get; set; }
    public List<SchoolTranscriptGradeDto> Grades { get; set; } = [];
}

public class SchoolTranscriptGradeDto
{
    public string GradeLevel { get; set; } = default!;
    public double? AverageScore { get; set; }
    public double? Gpa { get; set; }
    public List<string> ClassOptions { get; set; } = [];
    public List<TranscriptSubjectDto> Subjects { get; set; } = [];
    public int SubjectPage { get; set; }
    public int SubjectPageSize { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalSubjectPages { get; set; }
}

public class TranscriptSubjectDto
{
    public string Subject { get; set; } = default!;
    public string SubjectCode { get; set; } = default!;
    public List<string> CourseNames { get; set; } = [];
    public List<TranscriptClassScoreDto> ClassScores { get; set; } = [];
    public double? Semester1Final { get; set; }
    public double? Semester2Final { get; set; }
    public double? YearAverage { get; set; }
    public double Credit { get; set; } = 1;
}

public class TranscriptClassScoreDto
{
    public long CanvasCourseId { get; set; }
    public string ClassName { get; set; } = default!;
    public string CourseName { get; set; } = default!;
    public double? Semester1Final { get; set; }
    public double? Semester2Final { get; set; }
    public double? YearAverage { get; set; }
    public double Credit { get; set; }
    public int RecordCount { get; set; }
}

public class TranscriptSubjectOptionDto
{
    public string SubjectCode { get; set; } = default!;
    public string Subject { get; set; } = default!;
}

public class SchoolTranscriptGradeDetailDto
{
    public string AcademicYear { get; set; } = default!;
    public string GradeLevel { get; set; } = default!;
    public double? AverageScore { get; set; }
    public double? Gpa { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalRecords { get; set; }
    public int MatchingRecords { get; set; }
    public List<string> ClassOptions { get; set; } = [];
    public List<TranscriptSubjectOptionDto> SubjectOptions { get; set; } = [];
    public List<TranscriptSubjectDto> Subjects { get; set; } = [];
    public int SubjectPage { get; set; }
    public int SubjectPageSize { get; set; }
    public int TotalSubjectPages { get; set; }
    public List<TranscriptScoreRecordDto> Records { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class TranscriptScoreRecordDto
{
    public long WarehouseScoreFactId { get; set; }
    public long CanvasEnrollmentId { get; set; }
    public long CanvasUserId { get; set; }
    public long CanvasCourseId { get; set; }
    public int StudentProfileId { get; set; }
    public string SISUserId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string CourseName { get; set; } = default!;
    public string? CourseCode { get; set; }
    public long? EnrollmentTermId { get; set; }
    public string? EnrollmentTermName { get; set; }
    public long? GradingPeriodId { get; set; }
    public string? GradingPeriodTitle { get; set; }
    public string ScoreScope { get; set; } = default!;
    public double? Score { get; set; }
    public string ScoreSource { get; set; } = default!;
}
