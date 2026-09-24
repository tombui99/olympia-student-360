using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/imports/modules/{moduleId}/field-options")]
[ApiController]
[Authorize]
public sealed class StudentRecordFieldOptionsController(
    AppDbContext context,
    StudentRecordFieldOptionService options) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<StudentRecordFieldOptionDto>>> List(string moduleId)
    {
        if (!IsSupportedModule(moduleId)) return NotFound();
        return Ok((await options.ListAsync(moduleId)).Select(ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal")]
    public async Task<ActionResult<StudentRecordFieldOptionDto>> Create(
        string moduleId,
        SaveStudentRecordFieldOptionDto model)
    {
        var validation = Validate(moduleId, model);
        if (validation != null) return BadRequest(new { Message = validation });
        var value = model.Value.Trim();
        if (await Exists(moduleId, model.FieldKey, value))
            return Conflict(new { Message = "This dropdown option already exists." });

        var option = new StudentRecordFieldOption
        {
            ModuleId = moduleId,
            FieldKey = model.FieldKey,
            Value = value,
            OptionGroup = CleanGroup(model.OptionGroup),
            SortOrder = model.SortOrder
        };
        context.StudentRecordFieldOptions.Add(option);
        await context.SaveChangesAsync();
        return Ok(ToDto(option));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal")]
    public async Task<ActionResult<StudentRecordFieldOptionDto>> Update(
        string moduleId,
        int id,
        SaveStudentRecordFieldOptionDto model)
    {
        var option = await context.StudentRecordFieldOptions
            .FirstOrDefaultAsync(item => item.Id == id && item.ModuleId == moduleId);
        if (option == null) return NotFound();
        var validation = Validate(moduleId, model);
        if (validation != null) return BadRequest(new { Message = validation });
        var value = model.Value.Trim();
        if (await Exists(moduleId, model.FieldKey, value, id))
            return Conflict(new { Message = "This dropdown option already exists." });

        var previousValue = option.Value;
        option.FieldKey = model.FieldKey;
        option.Value = value;
        option.OptionGroup = CleanGroup(model.OptionGroup);
        option.SortOrder = model.SortOrder;
        option.UpdatedAt = DateTime.UtcNow;
        if (moduleId == StudentRecordFieldOptionService.BehaviourModule &&
            model.FieldKey == StudentRecordFieldOptionService.BehaviourTypeField)
        {
            var category = StudentRecordFieldOptionService.CategoryName(option.OptionGroup);
            await context.StudentBehaviours.Where(item => item.BehaviourType == previousValue)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.BehaviourType, value)
                    .SetProperty(item => item.ClassificationCategory, category));
        }
        else if (moduleId == StudentRecordFieldOptionService.ProjectModule &&
                 model.FieldKey == StudentRecordFieldOptionService.ProjectSubjectField)
        {
            await context.StudentProjects.Where(item => item.Subject == previousValue)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Subject, value));
        }
        else if (moduleId == StudentRecordFieldOptionService.ProjectModule &&
                 model.FieldKey == StudentRecordFieldOptionService.ProjectStatusField)
        {
            await context.StudentProjects.Where(item => item.Status == previousValue)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, value));
        }
        await context.SaveChangesAsync();
        return Ok(ToDto(option));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal")]
    public async Task<IActionResult> Delete(string moduleId, int id)
    {
        var option = await context.StudentRecordFieldOptions
            .FirstOrDefaultAsync(item => item.Id == id && item.ModuleId == moduleId);
        if (option == null) return NotFound();
        var hasAnother = await context.StudentRecordFieldOptions.AnyAsync(item =>
            item.ModuleId == moduleId && item.FieldKey == option.FieldKey && item.Id != id);
        if (!hasAnother)
            return BadRequest(new { Message = "A required dropdown must keep at least one option." });

        context.StudentRecordFieldOptions.Remove(option);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> Exists(string moduleId, string fieldKey, string value, int? exceptId = null) =>
        await context.StudentRecordFieldOptions.AnyAsync(item =>
            item.ModuleId == moduleId && item.FieldKey == fieldKey && item.Value == value &&
            (!exceptId.HasValue || item.Id != exceptId.Value));

    private static string? Validate(string moduleId, SaveStudentRecordFieldOptionDto model)
    {
        if (!StudentRecordFieldOptionService.IsConfigurable(moduleId, model.FieldKey))
            return "This standard field is not configurable.";
        if (string.IsNullOrWhiteSpace(model.Value)) return "Option value is required.";
        if (!StudentRecordFieldOptionService.IsValidGroup(moduleId, model.FieldKey, CleanGroup(model.OptionGroup)))
            return "Behaviour types must be grouped as positive or concerning.";
        var limit = model.FieldKey == StudentRecordFieldOptionService.ProjectStatusField ? 50
            : model.FieldKey == StudentRecordFieldOptionService.ProjectSubjectField ? 100 : 255;
        return model.Value.Trim().Length > limit ? $"Option value cannot exceed {limit} characters." : null;
    }

    private static bool IsSupportedModule(string moduleId) =>
        moduleId is StudentRecordFieldOptionService.BehaviourModule or StudentRecordFieldOptionService.ProjectModule;

    private static string? CleanGroup(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static StudentRecordFieldOptionDto ToDto(StudentRecordFieldOption option) => new()
    {
        Id = option.Id,
        ModuleId = option.ModuleId,
        FieldKey = option.FieldKey,
        Value = option.Value,
        OptionGroup = option.OptionGroup,
        SortOrder = option.SortOrder
    };
}
