namespace OlympiaStudent360.Api.Core.DTOs;

public class SISClassPageDto
{
    public List<SISClassListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<string> AcademicYears { get; set; } = [];
}

public class SISClassListItemDto
{
    public int Id { get; set; }
    public string SISClassCode { get; set; } = default!;
    public string ClassName { get; set; } = default!;
    public string AcademicYear { get; set; } = default!;
    public string? Term { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Campus { get; set; }
    public int TeacherCount { get; set; }
    public int StudentCount { get; set; }
}

public class SISClassDetailDto : SISClassListItemDto
{
    public List<SISClassTeacherDto> Teachers { get; set; } = [];
    public List<SISClassStudentDto> Students { get; set; } = [];
}

public class SISClassTeacherDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? AssignmentRole { get; set; }
}

public class SISClassStudentDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}
