using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class StudentSupportActionDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = default!;
    public string SISUserId { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
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
    public bool ShareWithFamily { get; set; }
    public string CreatedByName { get; set; } = default!;
    public string? UpdatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class StudentSupportActionsPageDto
{
    public List<StudentSupportActionDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int ActiveCount { get; set; }
    public int FollowUpDueCount { get; set; }
    public int GoalAchievedCount { get; set; }
    public List<string> AvailableAcademicYears { get; set; } = [];
}

public class StudentSupportJourneyPageDto : StudentSupportActionsPageDto
{
    public int LearningCount { get; set; }
    public int BehaviourCount { get; set; }
    public int MentalHealthCount { get; set; }
}

public class SaveStudentSupportActionDto
{
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required]
    public DateTime? ActionDate { get; set; }

    public DateTime? FollowUpDate { get; set; }

    [StringLength(20)]
    public string? AcademicYear { get; set; }

    [StringLength(50)]
    public string? Term { get; set; }

    [Required, StringLength(30)]
    public string Category { get; set; } = default!;

    [Required, StringLength(255)]
    public string Title { get; set; } = default!;

    [Required, StringLength(2000)]
    public string Goal { get; set; } = default!;

    [Required, StringLength(4000)]
    public string ActionTaken { get; set; } = default!;

    [StringLength(4000)]
    public string? Result { get; set; }

    [StringLength(2000)]
    public string? NextStep { get; set; }

    [Required, StringLength(30)]
    public string Status { get; set; } = default!;

    [Required, StringLength(30)]
    public string Progress { get; set; } = default!;

    [StringLength(100)]
    public string? Subject { get; set; }

    public bool ShareWithFamily { get; set; } = true;
}
