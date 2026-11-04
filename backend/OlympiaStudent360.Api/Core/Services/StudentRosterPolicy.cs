using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Current selections use SIS status; historical queries keep the complete profile table.</summary>
public static class StudentRosterPolicy
{
    public static IQueryable<StudentProfile> CurrentRoster(this IQueryable<StudentProfile> query, AppDbContext db) =>
        query.Where(student => !student.SISIsDeleted && (student.SISIsActive == true ||
            student.SISIsActive == null && student.SchoolOnlineUserId == null &&
            !db.SisSyncStates.Any(state => state.ProjectsCurrentRoster && state.LastSuccessfulWatermark != null)));
}
