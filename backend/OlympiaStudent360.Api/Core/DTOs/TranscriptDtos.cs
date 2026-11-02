namespace OlympiaStudent360.Api.Core.DTOs;

public class TranscriptCatalogDto
{
    public List<TranscriptCatalogYearDto> Years { get; set; } = [];
    public List<TranscriptSubjectOptionDto> SubjectOptions { get; set; } = [];
}

public class TranscriptCatalogYearDto
{
    public string AcademicYear { get; set; } = "";
    public List<string> GradeLevels { get; set; } = [];
}

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
    public TranscriptRecordDto Record { get; set; } = new();
    public bool CanEditRecord { get; set; }
    public string? TranscriptName { get; set; }
    public List<string> AcademicYearOptions { get; set; } = [];
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
    public bool ShowAverage { get; set; }
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
    public string? EnglishName { get; set; }
    public string? VietnameseName { get; set; }
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
    public List<string> GradeLevels { get; set; } = [];
}

public class TranscriptRecordDto
{
    public int Revision { get; set; }
    public string? TranscriptName { get; set; }
    public DateOnly? EnrollmentDate { get; set; }
    public DateOnly? GraduationDate { get; set; }
    public DateOnly? GraduationExamDate { get; set; }
    public DateOnly? IbReleaseDate { get; set; }
    public string? Notes { get; set; }
    public int? IbCorePoints { get; set; }
    public List<TranscriptYearRecordDto> Years { get; set; } = [];
}

public class TranscriptYearRecordDto
{
    public string AcademicYear { get; set; } = "";
    public string? GradeLevel { get; set; }
    public bool? ShowAverage { get; set; }
    public List<TranscriptCreditDto> Credits { get; set; } = [];
    public List<TranscriptIbSubjectDto> IbSubjects { get; set; } = [];
}

public class TranscriptCreditDto
{
    public string SubjectCode { get; set; } = "";
    public double Credit { get; set; } = 1;
    public string? EnglishName { get; set; }
    public string? VietnameseName { get; set; }
}

public class TranscriptIbSubjectDto
{
    public string SubjectCode { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string? Level { get; set; }
    public string? PredictedGrade { get; set; }
    public string? Term1 { get; set; }
    public string? Term2 { get; set; }
    public string? Term3 { get; set; }
    public string? Term4 { get; set; }
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
