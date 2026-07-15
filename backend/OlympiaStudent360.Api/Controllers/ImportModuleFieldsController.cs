using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

/// <summary>
/// Lets admins/principals design the custom fields on each import module. Reading the fields
/// is open to any authenticated user (the forms need them); editing is admin/principal only.
/// </summary>
[Route("api/imports/modules/{moduleId}/fields")]
[ApiController]
[Authorize]
public class ImportModuleFieldsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImportModuleFieldsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ImportModuleFieldDto>>> List(string moduleId)
    {
        if (!ImportCustomFieldService.IsKnownModule(moduleId))
        {
            return NotFound();
        }
        var fields = await _context.ImportModuleFields.AsNoTracking()
            .Where(f => f.ModuleId == moduleId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .ToListAsync();
        return Ok(fields.Select(ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Principal")]
    public async Task<ActionResult<ImportModuleFieldDto>> Create(string moduleId, SaveImportModuleFieldDto model)
    {
        if (!ImportCustomFieldService.IsKnownModule(moduleId))
        {
            return NotFound();
        }
        var validation = Validate(model);
        if (validation != null)
        {
            return BadRequest(new { Message = validation });
        }

        var key = string.IsNullOrWhiteSpace(model.FieldKey)
            ? ImportCustomFieldService.Slugify(model.Label)
            : ImportCustomFieldService.Slugify(model.FieldKey);
        key = await UniqueKey(moduleId, key);

        var field = new ImportModuleField
        {
            ModuleId = moduleId,
            FieldKey = key,
            Label = model.Label.Trim(),
            FieldType = model.FieldType,
            Required = model.Required,
            OptionsJson = SerializeOptions(model),
            SortOrder = model.SortOrder
        };
        _context.ImportModuleFields.Add(field);
        await _context.SaveChangesAsync();
        return Ok(ToDto(field));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Principal")]
    public async Task<ActionResult<ImportModuleFieldDto>> Update(string moduleId, int id, SaveImportModuleFieldDto model)
    {
        var field = await _context.ImportModuleFields.FirstOrDefaultAsync(f => f.Id == id && f.ModuleId == moduleId);
        if (field == null)
        {
            return NotFound();
        }
        var validation = Validate(model);
        if (validation != null)
        {
            return BadRequest(new { Message = validation });
        }

        field.Label = model.Label.Trim();
        field.FieldType = model.FieldType;
        field.Required = model.Required;
        field.OptionsJson = SerializeOptions(model);
        field.SortOrder = model.SortOrder;
        field.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToDto(field));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Principal")]
    public async Task<IActionResult> Delete(string moduleId, int id)
    {
        var field = await _context.ImportModuleFields.FirstOrDefaultAsync(f => f.Id == id && f.ModuleId == moduleId);
        if (field == null)
        {
            return NotFound();
        }
        _context.ImportModuleFields.Remove(field);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? Validate(SaveImportModuleFieldDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Label))
        {
            return "Label is required.";
        }
        if (!ImportCustomFieldService.FieldTypes.Contains(model.FieldType))
        {
            return "Unsupported field type.";
        }
        if (model.FieldType == "select" && model.Options.Count(o => !string.IsNullOrWhiteSpace(o)) == 0)
        {
            return "A dropdown field needs at least one option.";
        }
        return null;
    }

    private static string? SerializeOptions(SaveImportModuleFieldDto model)
    {
        if (model.FieldType != "select")
        {
            return null;
        }
        var options = model.Options
            .Select(o => o.Trim())
            .Where(o => !string.IsNullOrEmpty(o))
            .Distinct()
            .ToList();
        return JsonSerializer.Serialize(options);
    }

    private async Task<string> UniqueKey(string moduleId, string baseKey)
    {
        var existing = await _context.ImportModuleFields
            .Where(f => f.ModuleId == moduleId)
            .Select(f => f.FieldKey)
            .ToListAsync();
        var key = baseKey;
        var suffix = 2;
        while (existing.Contains(key))
        {
            key = $"{baseKey}_{suffix++}";
        }
        return key;
    }

    private static ImportModuleFieldDto ToDto(ImportModuleField field) => new()
    {
        Id = field.Id,
        ModuleId = field.ModuleId,
        FieldKey = field.FieldKey,
        Label = field.Label,
        FieldType = field.FieldType,
        Required = field.Required,
        Options = ImportCustomFieldService.ParseOptions(field.OptionsJson),
        SortOrder = field.SortOrder
    };
}
