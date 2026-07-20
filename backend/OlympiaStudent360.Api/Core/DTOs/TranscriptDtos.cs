namespace OlympiaStudent360.Api.Core.DTOs;

public class SchoolTranscriptDto
{
    public double? OverallAverage { get; set; }
    public int TotalAcademicYears { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
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
    public List<string> Terms { get; set; } = [];
    public List<TranscriptSubjectDto> Subjects { get; set; } = [];
}

public class TranscriptSubjectDto
{
    public string Subject { get; set; } = default!;
    public Dictionary<string, double?> TermScores { get; set; } = [];
    public double? YearAverage { get; set; }
}

public class SchoolTranscriptGradeDetailDto
{
    public string AcademicYear { get; set; } = default!;
    public string GradeLevel { get; set; } = default!;
    public double? AverageScore { get; set; }
    public int TotalStudents { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalRecords { get; set; }
    public int MatchingRecords { get; set; }
    public List<string> Terms { get; set; } = [];
    public List<TranscriptSubjectDto> Subjects { get; set; } = [];
    public List<TranscriptScoreRecordDto> Records { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class TranscriptScoreRecordDto
{
    public long CanvasScoreId { get; set; }
    public int StudentProfileId { get; set; }
    public string StudentCode { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string CourseName { get; set; } = default!;
    public string Term { get; set; } = default!;
    public double Score { get; set; }
    public string ScoreSource { get; set; } = default!;
    public DateTime RecordedAt { get; set; }
}
