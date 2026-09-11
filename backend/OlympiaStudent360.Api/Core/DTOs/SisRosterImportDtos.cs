namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class SisRosterImportResultDto
{
    public int ImportHistoryId { get; set; }
    public int StudentsImported { get; set; }
    public int StudentsAdded { get; set; }
    public int StudentsUpdated { get; set; }
    public int StudentsRemoved { get; set; }
    public int ParentsImported { get; set; }
    public int CanvasLinkedStudents { get; set; }
    public int CanvasFactsRemoved { get; set; }
}
