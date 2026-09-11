using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/imports/sis-students")]
[ApiController]
[Authorize(Roles = "Admin,SchoolAdministration,Principal")]
public sealed class SisRosterImportsController(
    AppDbContext db,
    SisRosterWorkbookReader reader,
    SisRosterImportService importer) : ControllerBase
{
    private const long MaximumFileSize = 20 * 1024 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSize)]
    public async Task<ActionResult<SisRosterImportResultDto>> Import(
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0 || files.Any(file => file.Length == 0))
            return BadRequest(new { Message = "Choose one or more non-empty SIS Excel workbooks." });
        if (files.Sum(file => file.Length) > MaximumFileSize)
            return BadRequest(new { Message = "The combined SIS workbooks must be 20 MB or smaller." });
        if (files.Any(file => !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { Message = "The SIS roster import accepts .xlsx workbooks only." });

        try
        {
            var workbooks = new List<SisRosterWorkbook>();
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();
                workbooks.Add(reader.Read(stream));
            }
            var workbook = SisRosterWorkbooks.Combine(workbooks);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var username = User.Identity?.Name ?? "unknown";
            var fullName = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            var result = await importer.Import(
                workbook,
                string.Join(" + ", files.Select(file => Path.GetFileName(file.FileName))),
                new SisRosterImportActor(userId, username, fullName ?? username),
                cancellationToken);
            return Created($"/api/import-history/{result.ImportHistoryId}", new SisRosterImportResultDto
            {
                ImportHistoryId = result.ImportHistoryId,
                StudentsImported = result.StudentsImported,
                StudentsAdded = result.StudentsAdded,
                StudentsUpdated = result.StudentsUpdated,
                StudentsRemoved = result.StudentsRemoved,
                ParentsImported = result.ParentsImported,
                CanvasLinkedStudents = result.CanvasLinkedStudents,
                CanvasFactsRemoved = result.CanvasFactsRemoved
            });
        }
        catch (SisRosterValidationException exception)
        {
            return BadRequest(new
            {
                Message = "No students were imported. Correct the SIS workbook errors and try again.",
                exception.Errors
            });
        }
        catch (DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return BadRequest(new { Message = "The uploaded file is not a readable Excel workbook." });
        }
    }
}
