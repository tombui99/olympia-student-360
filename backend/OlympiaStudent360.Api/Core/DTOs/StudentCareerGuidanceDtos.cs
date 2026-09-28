using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class StudentCareerGuidanceRecordDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = default!;
    public string SISUserId { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
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
    public string RecordedByName { get; set; } = default!;
    public string? UpdatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class StudentCareerGuidancePageDto
{
    public List<StudentCareerGuidanceRecordDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int ActiveCount { get; set; }
    public int InProgressCount { get; set; }
    public int NeedsAttentionCount { get; set; }
    public int CompletedCount { get; set; }
    public List<string> AvailableAcademicYears { get; set; } = [];
}

public sealed class SaveStudentCareerGuidanceRecordDto
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required, StringLength(40)]
    public string RecordType { get; set; } = default!;

    [Required]
    public DateTime? RecordDate { get; set; }

    [StringLength(20)]
    public string? AcademicYear { get; set; }

    [StringLength(50)]
    public string? Term { get; set; }

    [Required, StringLength(255)]
    public string Title { get; set; } = default!;

    [Required, StringLength(30)]
    public string Status { get; set; } = default!;

    [StringLength(2000)]
    public string? TargetCountries { get; set; }

    [StringLength(4000)]
    public string? TargetUniversities { get; set; }

    [StringLength(2000)]
    public string? TargetMajors { get; set; }

    [StringLength(4000)]
    public string? CareerInterests { get; set; }

    [Range(0, 100)]
    public int? ApplicationProgressPercent { get; set; }

    [Range(0, 100)]
    public int? PreparationProgressPercent { get; set; }

    [Range(1, 5)]
    public int? QualityRating { get; set; }

    [StringLength(4000)]
    public string? ActivitiesSummary { get; set; }

    [StringLength(4000)]
    public string? Issues { get; set; }

    [StringLength(4000)]
    public string? Solutions { get; set; }

    [StringLength(4000)]
    public string? StudentSupport { get; set; }

    [StringLength(4000)]
    public string? StudentFeedback { get; set; }

    [StringLength(4000)]
    public string? NextSteps { get; set; }

    [StringLength(4000)]
    public string? Recommendations { get; set; }

    [StringLength(4000)]
    public string? Proposals { get; set; }

    [StringLength(255)]
    public string? InternshipCompany { get; set; }

    [StringLength(255)]
    public string? InternshipRole { get; set; }

    public DateTime? InternshipStartDate { get; set; }
    public DateTime? InternshipEndDate { get; set; }

    [Range(0, 100)]
    public int? InternshipProgressPercent { get; set; }

    [Range(1, 5)]
    public int? InternshipQualityRating { get; set; }

    [StringLength(4000)]
    public string? StudentSelfAssessment { get; set; }

    [StringLength(4000)]
    public string? HostAssessment { get; set; }

    [StringLength(2000)]
    public string? FinalProduct { get; set; }
}
