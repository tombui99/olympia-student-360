namespace OlympiaStudent360.Api.Core.DTOs;

public sealed record SisTranscriptStudentDto(int Id, string FullName, string StudentCode, string? Homeroom);

public sealed record SisTranscriptDirectoryDto(
    List<SisTranscriptStudentDto> Students, int Page, int TotalPages);

public sealed record SisTranscriptDto(SisTranscriptStudentDto Student, List<SisTranscriptMarkDto> Marks);

public sealed record SisTranscriptMarkDto(
    string SchoolYearId, string SchoolYear, string GradeLevel,
    string SubjectId, string Subject, string GradeItemId, string GradeItem,
    string Semester, string Stage, string? PeriodName, string? Homeroom,
    decimal? Score, string? Letter, decimal? MaxScore, string? Comment);

public sealed record SisTranscriptYearDto(string Id, string Name, string[] Grades);

public sealed record SisTranscriptSummaryDto(int Students, SisTranscriptSubjectSummaryDto[] Subjects);

public sealed record SisTranscriptSubjectSummaryDto(string Id, string Name, SisTranscriptItemSummaryDto[] Items);

public sealed record SisTranscriptItemSummaryDto(
    string Item, string Semester, string Stage, string? PeriodName, int Students,
    decimal? Average, decimal? MaxScore, SisTranscriptRatingDto[] Ratings);

public sealed record SisTranscriptRatingDto(string Letter, int Count);
