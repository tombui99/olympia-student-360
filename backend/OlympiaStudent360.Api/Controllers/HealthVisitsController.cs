using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/health-visits")]
[ApiController]
[Authorize]
public class HealthVisitsController : ControllerBase
{
    private readonly AppDbContext _context;

    public HealthVisitsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SISHealthVisitsPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? className = null,
        [FromQuery] string? classification = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
        {
            return BadRequest("The start date cannot be later than the end date.");
        }

        var allVisits = _context.SISHealthVisits.AsNoTracking();
        var availableClasses = await allVisits
            .Where(visit => visit.ClassName != null && visit.ClassName != "")
            .Select(visit => visit.ClassName!)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync();
        var availableClassifications = await allVisits
            .Where(visit => visit.Classification != null && visit.Classification != "")
            .Select(visit => visit.Classification!)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync();

        var query = allVisits;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(visit =>
                visit.FullName.Contains(value) ||
                visit.SISUserId != null && visit.SISUserId.Contains(value) ||
                visit.ClassName != null && visit.ClassName.Contains(value) ||
                visit.Symptoms != null && visit.Symptoms.Contains(value) ||
                visit.Diagnosis != null && visit.Diagnosis.Contains(value) ||
                visit.ExaminedBy != null && visit.ExaminedBy.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(className))
        {
            var value = className.Trim();
            query = query.Where(visit => visit.ClassName == value);
        }

        if (!string.IsNullOrWhiteSpace(classification))
        {
            var value = classification.Trim();
            query = query.Where(visit => visit.Classification == value);
        }

        if (from.HasValue)
        {
            var start = from.Value.Date;
            query = query.Where(visit => visit.ExamTime >= start);
        }

        if (to.HasValue)
        {
            var endExclusive = to.Value.Date.AddDays(1);
            query = query.Where(visit => visit.ExamTime < endExclusive);
        }

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));

        var items = await query
            .OrderByDescending(visit => visit.ExamTime)
            .ThenByDescending(visit => visit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(visit => new SISHealthVisitDto
            {
                Id = visit.Id,
                StudentProfileId = visit.Student != null ? visit.Student.Id : null,
                SISUserId = visit.SISUserId,
                ExamTime = visit.ExamTime,
                FullName = visit.FullName,
                Gender = visit.Gender,
                ClassName = visit.ClassName,
                Symptoms = visit.Symptoms,
                Diagnosis = visit.Diagnosis,
                Classification = visit.Classification,
                Treatment = visit.Treatment,
                Medication = visit.Medication,
                ExaminedBy = visit.ExaminedBy
            })
            .ToListAsync();

        return Ok(new SISHealthVisitsPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            AvailableClasses = availableClasses,
            AvailableClassifications = availableClassifications
        });
    }
}
