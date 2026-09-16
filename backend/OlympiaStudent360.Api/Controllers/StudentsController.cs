using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/students")]
[ApiController]
[Authorize]
public class StudentsController : ControllerBase
{
    private sealed record StudentSignalRow(
        StudentProfile Student,
        StudentSignalSummary Signals,
        DateTime? LatestCanvasScoreDate);

    private readonly AppDbContext _context;
    private readonly CanvasReportingPeriodService _canvasReportingPeriods;

    public StudentsController(AppDbContext context, CanvasReportingPeriodService canvasReportingPeriods)
    {
        _context = context;
        _canvasReportingPeriods = canvasReportingPeriods;
    }

    [HttpGet]
    public async Task<ActionResult<StudentsPageDto>> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? grade = null,
        [FromQuery] bool atRiskOnly = false)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var visible = VisibleStudents();
        var availableGrades = (await visible
            .Where(student => (student.GradeLevel ?? student.CanvasGradeLevel) != null &&
                (student.GradeLevel ?? student.CanvasGradeLevel) != "")
            .Select(student => (student.GradeLevel ?? student.CanvasGradeLevel)!)
            .Distinct()
            .ToListAsync())
            .OrderBy(value => int.TryParse(value, out var number) ? number : int.MaxValue)
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var query = visible;
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term[..Math.Min(term.Length, 100)];
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.SISUserId, pattern, "!") ||
                student.SISStudentCode != null && EF.Functions.Like(student.SISStudentCode, pattern, "!") ||
                student.Email != null && EF.Functions.Like(student.Email, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"));
        }
        if (!string.IsNullOrWhiteSpace(grade))
        {
            var normalizedGrade = grade.Trim();
            query = normalizedGrade == "—"
                ? query.Where(student => (student.GradeLevel ?? student.CanvasGradeLevel) == null ||
                    (student.GradeLevel ?? student.CanvasGradeLevel) == "")
                : query.Where(student => (student.GradeLevel ?? student.CanvasGradeLevel) == normalizedGrade);
        }

        var settings = await ClassificationSettings();
        var canvasPeriod = await _canvasReportingPeriods.ResolveAsync();
        var riskSince = DateTime.UtcNow.AddDays(-settings.RiskWindowDays);
        if (atRiskOnly)
        {
            var atRiskIds = (await LoadVisibleSignals(settings, canvasPeriod))
                .Where(entry => StudentClassificationService.ClassifyRisk(entry.Signals, settings).Level != "None")
                .Select(entry => entry.Student.Id)
                .ToList();
            query = query.Where(student => atRiskIds.Contains(student.Id));
        }

        var rows = query.Select(student => new
        {
            student.Id,
            student.SISStudentCode,
            student.SISUserId,
            student.FullName,
            GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
            student.Homeroom,
            student.Campus,
            student.EducationLevel,
            student.Program,
            BehaviourCount = student.Behaviours.Count,
            ProjectCount = student.Projects.Count,
            MentalHealthCount = student.MentalHealthRecords.Count,
            BusViolationCount = student.BusAttendanceRecords.Count(record => record.AttendanceDate >= riskSince)
        });
        var totalCount = await rows.CountAsync();
        var totalPages = TotalPages(totalCount, pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var pageRows = await rows
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.SISUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var pageStudentIds = pageRows.Select(student => student.Id).ToList();
        var canvasAverages = await CanvasAverages(pageStudentIds, canvasPeriod);
        var behaviourRows = await _context.StudentBehaviours.AsNoTracking()
            .Where(behaviour => pageStudentIds.Contains(behaviour.StudentProfileId))
            .Select(behaviour => new { behaviour.StudentProfileId, behaviour.BehaviourType, behaviour.IncidentDate })
            .ToListAsync();
        var behaviourCounts = behaviourRows.GroupBy(behaviour => behaviour.StudentProfileId)
            .ToDictionary(group => group.Key, group => new
            {
                Positive = group.Count(behaviour => StudentClassificationService.IsCommendationBehaviourType(behaviour.BehaviourType)),
                Concerning = group.Count(behaviour => StudentClassificationService.IsConcerningBehaviourType(behaviour.BehaviourType)),
                RecentConcerning = group.Count(behaviour => behaviour.IncidentDate >= riskSince
                    && StudentClassificationService.IsConcerningBehaviourType(behaviour.BehaviourType))
            });

        return Ok(new StudentsPageDto
        {
            Items = pageRows.Select(student =>
            {
                var counts = behaviourCounts.GetValueOrDefault(student.Id);
                var risk = StudentClassificationService.ClassifyRisk(
                    new StudentSignalSummary(
                        counts?.RecentConcerning ?? 0,
                        0,
                        canvasAverages.GetValueOrDefault(student.Id),
                        student.BusViolationCount,
                        0),
                    settings);
                return new StudentListItemDto
                {
                    Id = student.Id, SISStudentCode = student.SISStudentCode,
                    SISUserId = student.SISUserId, FullName = student.FullName,
                    GradeLevel = student.GradeLevel, Homeroom = student.Homeroom,
                    Campus = student.Campus, EducationLevel = student.EducationLevel, Program = student.Program,
                    BehaviourCount = student.BehaviourCount,
                    PositiveBehaviourCount = counts?.Positive ?? 0,
                    ConcerningBehaviourCount = counts?.Concerning ?? 0,
                    ProjectCount = student.ProjectCount, MentalHealthCount = student.MentalHealthCount,
                    RiskLevel = risk.Level, RiskFlagCount = risk.Flags.Count
                };
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            AvailableGrades = availableGrades
        });
    }

    [HttpGet("at-risk")]
    public async Task<ActionResult<AtRiskStudentsPageDto>> GetAtRiskStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? term = null)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var settings = await ClassificationSettings();
        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm[..Math.Min(searchTerm.Length, 100)];
        }
        var normalizedGrade = grade?.Trim();
        var canvasPeriod = await _canvasReportingPeriods.ResolveAsync(academicYear, term);
        var candidates = (await LoadVisibleSignals(settings, canvasPeriod, searchTerm, normalizedGrade))
            .Select(entry => new
            {
                entry.Student,
                entry.Signals,
                Risk = StudentClassificationService.ClassifyRisk(entry.Signals, settings)
            })
            .Where(entry => entry.Risk.Level != "None");
        var atRiskStudents = candidates
            .OrderByDescending(entry => entry.Risk.Level == "High")
            .ThenByDescending(entry => entry.Signals.ConcerningBehaviourCount + entry.Signals.RiskBusViolationCount)
            .ThenBy(entry => entry.Student.FullName)
            .ToList();

        var totalCount = atRiskStudents.Count;
        return Ok(new AtRiskStudentsPageDto
        {
            Items = atRiskStudents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new AtRiskStudentDto
                {
                    Id = entry.Student.Id,
                    SISUserId = entry.Student.SISUserId,
                    FullName = entry.Student.FullName,
                    GradeLevel = entry.Student.GradeLevel ?? entry.Student.CanvasGradeLevel,
                    Homeroom = entry.Student.Homeroom,
                    BehaviourCount = entry.Student.Behaviours.Count,
                    PositiveBehaviourCount = entry.Student.Behaviours.Count(StudentClassificationService.IsCommendationBehaviour),
                    ConcerningBehaviourCount = entry.Student.Behaviours.Count(StudentClassificationService.IsConcerningBehaviour),
                    ProjectCount = entry.Student.Projects.Count,
                    MentalHealthCount = entry.Student.MentalHealthRecords.Count,
                    RiskLevel = entry.Risk.Level,
                    RiskFlagCount = entry.Risk.Flags.Count,
                    AverageCanvasScorePercentage = entry.Signals.AverageCanvasGrade,
                    Flags = entry.Risk.Flags
                })
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = TotalPages(totalCount, pageSize),
            HighRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "High"),
            MediumRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "Medium"),
            AcademicYear = canvasPeriod.AcademicYear,
            Term = canvasPeriod.Term
        });
    }

    [HttpGet("classification-settings")]
    public async Task<ActionResult<StudentClassificationSettingsDto>> GetClassificationSettings() =>
        Ok(await ClassificationSettings());

    [HttpPut("classification-settings")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal")]
    public async Task<ActionResult<StudentClassificationSettingsDto>> SaveClassificationSettings(
        StudentClassificationSettingsDto model)
    {
        ValidateClassificationSettings(model);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var setting = await _context.StudentClassificationSettings.OrderBy(item => item.Id).FirstOrDefaultAsync();
        if (setting == null)
        {
            setting = new StudentClassificationSetting();
            _context.StudentClassificationSettings.Add(setting);
        }
        setting.RiskWindowDays = model.RiskWindowDays;
        setting.HighRiskBehaviourCount = model.HighRiskBehaviourCount;
        setting.HighRiskBusViolationCount = model.HighRiskBusViolationCount;
        setting.HighRiskCanvasAverageBelow = model.HighRiskCanvasAverageBelow;
        setting.CommendationWindowDays = model.CommendationWindowDays;
        setting.CommendationPositiveBehaviourCount = model.CommendationPositiveBehaviourCount;
        setting.CommendationCanvasAverageAtLeast = model.CommendationCanvasAverageAtLeast;
        setting.CommendationMaxBusViolations = model.CommendationMaxBusViolations;
        setting.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToSettingsDto(setting));
    }

    [HttpGet("commendations")]
    public async Task<ActionResult<StudentCommendationsPageDto>> GetCommendations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? grade = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? term = null)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var settings = await ClassificationSettings();
        var commendationSince = DateTime.UtcNow.AddDays(-settings.CommendationWindowDays);
        var canvasPeriod = await _canvasReportingPeriods.ResolveAsync(academicYear, term);
        var commendations = (await LoadVisibleSignals(settings, canvasPeriod))
            .Select(entry => new
            {
                entry.Student,
                entry.Signals,
                entry.LatestCanvasScoreDate,
                Recognition = StudentClassificationService.ClassifyRecognition(entry.Signals, settings),
                PositiveBehaviours = entry.Student.Behaviours
                    .Where(StudentClassificationService.IsCommendationBehaviour)
                    .Where(behaviour => behaviour.IncidentDate >= commendationSince)
                    .OrderByDescending(behaviour => behaviour.IncidentDate)
                    .ToList()
            })
            .Where(entry => entry.Recognition.Qualifies)
            .Select(entry => new StudentCommendationDto
            {
                Id = entry.Student.Id,
                StudentId = entry.Student.Id,
                SISUserId = entry.Student.SISUserId,
                StudentName = entry.Student.FullName,
                GradeLevel = entry.Student.GradeLevel ?? entry.Student.CanvasGradeLevel,
                Homeroom = entry.Student.Homeroom,
                IncidentDate = new[]
                    {
                        entry.PositiveBehaviours.FirstOrDefault()?.IncidentDate,
                        entry.Recognition.HighCanvasGrade ? entry.LatestCanvasScoreDate : null
                    }
                    .Where(date => date.HasValue).Max(date => date!.Value),
                CommendationType = "Multi-source recognition",
                Notes = entry.PositiveBehaviours.FirstOrDefault()?.Notes,
                RecordedByName = entry.PositiveBehaviours.FirstOrDefault()?.RecordedByName,
                PositiveBehaviourCount = entry.Signals.PositiveBehaviourCount,
                AverageCanvasGrade = entry.Signals.AverageCanvasGrade,
                BusViolationCount = entry.Signals.CommendationBusViolationCount
            })
            .ToList();

        var availableGrades = commendations
            .Select(commendation => commendation.GradeLevel)
            .Where(gradeLevel => !string.IsNullOrWhiteSpace(gradeLevel))
            .Select(gradeLevel => gradeLevel!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(gradeLevel => gradeLevel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = commendations.Where(commendation =>
            (string.IsNullOrWhiteSpace(search) ||
             commendation.StudentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             commendation.SISUserId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             commendation.CommendationType.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(grade) || string.Equals(commendation.GradeLevel, grade, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(commendation => commendation.IncidentDate)
            .ThenBy(commendation => commendation.StudentName)
            .ToList();

        var totalCount = filtered.Count;
        return Ok(new StudentCommendationsPageDto
        {
            Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = TotalPages(totalCount, pageSize),
            TotalCommendationCount = commendations.Count,
            StudentsRecognisedCount = commendations.Count,
            RecentCount = commendations.Count(commendation => commendation.IncidentDate >= DateTime.UtcNow.AddDays(-30)),
            AvailableGrades = availableGrades,
            AcademicYear = canvasPeriod.AcademicYear,
            Term = canvasPeriod.Term
        });
    }

    [HttpGet("manual-entry-options")]
    public async Task<ActionResult<ManualEntryOptionsDto>> GetManualEntryOptions()
    {
        var academicYears = await _context.AcademicPeriods.AsNoTracking()
            .Select(period => period.AcademicYear)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync();
        return Ok(new ManualEntryOptionsDto
        {
            PositiveBehaviourTypes = StudentClassificationService.PositiveBehaviourTypes,
            ConcerningBehaviourTypes = StudentClassificationService.ConcerningBehaviourTypes,
            ProjectSubjects = StudentDataEntryOptions.ProjectSubjects,
            ProjectStatuses = StudentDataEntryOptions.ProjectStatuses,
            AcademicYears = academicYears,
            MentalHealthConcernTypes = StudentDataEntryOptions.MentalHealthConcernTypes,
            MentalHealthRiskLevels = StudentDataEntryOptions.MentalHealthRiskLevels,
            BusTripDirections = StudentDataEntryOptions.BusTripDirections,
            BusViolationTypes = StudentDataEntryOptions.BusViolationTypes
        });
    }

    [HttpGet("manual-entry-students")]
    public async Task<ActionResult<List<ManualEntryStudentDto>>> SearchManualEntryStudents(
        [FromQuery] string? query)
    {
        const int resultLimit = 20;
        var term = query?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            return Ok(new List<ManualEntryStudentDto>());
        }

        term = term[..Math.Min(term.Length, 100)];
        var pattern = $"%{EscapeLikePattern(term)}%";
        var students = await VisibleStudents()
            .Where(student =>
                EF.Functions.Like(student.SISUserId, pattern, "!") ||
                EF.Functions.Like(student.FullName, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"))
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.SISUserId)
            .Take(resultLimit)
            .Select(student => new ManualEntryStudentDto
            {
                Id = student.Id,
                SISUserId = student.SISUserId,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
                Homeroom = student.Homeroom
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpPost("{studentId:int}/behaviours")]
    public async Task<ActionResult<StudentBehaviourDto>> CreateBehaviour(
        int studentId,
        [FromBody] CreateStudentBehaviourDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(candidate => candidate.Id == studentId);
        if (student == null)
        {
            return BadRequest(new { Message = "The selected student ID is invalid." });
        }

        if (!StudentClassificationService.TryCanonicalBehaviourType(model.BehaviourType, out var behaviourType))
        {
            return BadRequest(new { Message = "The selected behaviour type is invalid." });
        }

        var (customError, customJson) = await BuildCustomFields("student-behaviours", model.CustomFields);
        if (customError != null)
        {
            return customError;
        }

        var recordedByName = await CurrentUserDisplayName();
        var behaviour = new StudentBehaviour
        {
            StudentProfileId = student.Id,
            IncidentDate = DateTime.SpecifyKind(model.IncidentDate!.Value, DateTimeKind.Utc),
            BehaviourType = behaviourType,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            RecordedByName = recordedByName,
            AcademicYear = Clean(model.AcademicYear), Term = Clean(model.Term),
            MeasurementCycle = Clean(model.MeasurementCycle), Program = Clean(model.Program), Subject = Clean(model.Subject),
            CustomFieldsJson = customJson
        };

        var history = CreateManualImportHistory("student-behaviours", recordedByName);
        behaviour.ImportHistory = history;
        _context.StudentBehaviours.Add(behaviour);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();

        return Created($"/api/students/{student.Id}", new StudentBehaviourDto
        {
            Id = behaviour.Id,
            IncidentDate = behaviour.IncidentDate,
            BehaviourType = StudentClassificationService.CanonicalBehaviourTypeOrOriginal(behaviour.BehaviourType),
            Category = StudentClassificationService.BehaviourCategory(behaviour.BehaviourType),
            Notes = behaviour.Notes,
            RecordedByName = behaviour.RecordedByName,
            AcademicYear = behaviour.AcademicYear, Term = behaviour.Term,
            MeasurementCycle = behaviour.MeasurementCycle, Program = behaviour.Program, Subject = behaviour.Subject
        });
    }

    [HttpPost("{studentId:int}/projects")]
    public async Task<ActionResult<StudentProjectDto>> CreateProject(
        int studentId,
        [FromBody] CreateStudentProjectDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(candidate => candidate.Id == studentId);
        if (student == null)
        {
            return BadRequest(new { Message = "The selected student ID is invalid." });
        }

        var subject = StudentDataEntryOptions.ProjectSubjects.FirstOrDefault(value =>
            string.Equals(value, model.Subject, StringComparison.OrdinalIgnoreCase));
        var status = StudentDataEntryOptions.ProjectStatuses.FirstOrDefault(value =>
            string.Equals(value, model.Status, StringComparison.OrdinalIgnoreCase));
        if (subject == null || status == null)
        {
            return BadRequest(new { Message = "The selected project subject or status is invalid." });
        }

        var (customError, customJson) = await BuildCustomFields("student-projects", model.CustomFields);
        if (customError != null)
        {
            return customError;
        }

        var project = new StudentProject
        {
            StudentProfileId = student.Id,
            ProjectName = model.ProjectName.Trim(),
            Subject = subject,
            Status = status,
            TeacherNotes = string.IsNullOrWhiteSpace(model.TeacherNotes) ? null : model.TeacherNotes.Trim(),
            AcademicYear = Clean(model.AcademicYear), Term = Clean(model.Term),
            MeasurementCycle = Clean(model.MeasurementCycle), Program = Clean(model.Program),
            ActivityDate = model.ActivityDate.HasValue ? DateTime.SpecifyKind(model.ActivityDate.Value, DateTimeKind.Utc) : null,
            CustomFieldsJson = customJson
        };

        var history = CreateManualImportHistory("student-projects", await CurrentUserDisplayName());
        project.ImportHistory = history;
        _context.StudentProjects.Add(project);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();

        return Created($"/api/students/{student.Id}", new StudentProjectDto
        {
            Id = project.Id,
            ProjectName = project.ProjectName,
            Subject = project.Subject,
            Status = project.Status,
            TeacherNotes = project.TeacherNotes,
            CreatedAt = project.CreatedAt,
            AcademicYear = project.AcademicYear, Term = project.Term,
            MeasurementCycle = project.MeasurementCycle, Program = project.Program, ActivityDate = project.ActivityDate
        });
    }

    [HttpPost("{studentId:int}/mental-health")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<StudentMentalHealthRecordDto>> CreateMentalHealth(
        int studentId,
        [FromBody] CreateStudentMentalHealthRecordDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(candidate => candidate.Id == studentId);
        if (student == null)
        {
            return BadRequest(new { Message = "The selected student ID is invalid." });
        }

        if (!await _context.AcademicPeriods.AsNoTracking()
                .AnyAsync(period => period.AcademicYear == model.AcademicYear.Trim()))
        {
            return BadRequest(new { Message = "The selected academic year is not configured." });
        }

        var concernType = Canonical(StudentDataEntryOptions.MentalHealthConcernTypes, model.ConcernType);
        var riskLevel = Canonical(StudentDataEntryOptions.MentalHealthRiskLevels, model.RiskLevel);
        if (concernType == null || riskLevel == null)
        {
            return BadRequest(new { Message = "The selected mental health options are invalid." });
        }

        var (customError, customJson) = await BuildCustomFields("student-mental-health", model.CustomFields);
        if (customError != null)
        {
            return customError;
        }

        var recordedByName = await CurrentUserDisplayName();
        var record = new StudentMentalHealthRecord
        {
            StudentProfileId = student.Id,
            RecordDate = DateTime.SpecifyKind(model.RecordDate!.Value, DateTimeKind.Utc),
            AcademicYear = model.AcademicYear.Trim(),
            ConcernType = concernType,
            RiskLevel = riskLevel,
            Notes = Clean(model.Notes),
            RecordedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            RecordedByName = recordedByName,
            CustomFieldsJson = customJson
        };

        var history = CreateManualImportHistory("student-mental-health", recordedByName);
        record.ImportHistory = history;
        _context.StudentMentalHealthRecords.Add(record);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/students/{student.Id}", ToMentalHealthDto(record));
    }

    [HttpPost("{studentId:int}/bus-attendance")]
    public async Task<ActionResult<StudentBusAttendanceRecordDto>> CreateBusAttendance(
        int studentId,
        [FromBody] CreateStudentBusAttendanceRecordDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(candidate => candidate.Id == studentId);
        if (student == null)
        {
            return BadRequest(new { Message = "The selected student ID is invalid." });
        }

        var direction = Canonical(StudentDataEntryOptions.BusTripDirections, model.TripDirection);
        var violation = Canonical(StudentDataEntryOptions.BusViolationTypes, model.ViolationType);
        if (direction == null || violation == null)
        {
            return BadRequest(new { Message = "The selected bus trip direction or violation type is invalid." });
        }

        var (customError, customJson) = await BuildCustomFields("student-bus-attendance", model.CustomFields);
        if (customError != null) return customError;

        var recordedByName = await CurrentUserDisplayName();
        var record = new StudentBusAttendanceRecord
        {
            StudentProfileId = student.Id,
            AttendanceDate = DateTime.SpecifyKind(model.AttendanceDate!.Value, DateTimeKind.Utc),
            TripDirection = direction,
            ViolationType = violation,
            BusCode = Clean(model.BusCode),
            RouteName = Clean(model.RouteName),
            Notes = Clean(model.Notes),
            RecordedByName = recordedByName,
            AcademicYear = Clean(model.AcademicYear),
            Term = Clean(model.Term),
            CustomFieldsJson = customJson
        };

        var history = CreateManualImportHistory("student-bus-attendance", recordedByName);
        record.ImportHistory = history;
        _context.StudentBusAttendanceRecords.Add(record);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/students/{student.Id}", new StudentBusAttendanceRecordDto
        {
            Id = record.Id,
            AttendanceDate = record.AttendanceDate,
            TripDirection = record.TripDirection,
            ViolationType = record.ViolationType,
            BusCode = record.BusCode,
            RouteName = record.RouteName,
            Notes = record.Notes,
            RecordedByName = record.RecordedByName,
            AcademicYear = record.AcademicYear,
            Term = record.Term,
            CustomFields = ImportCustomFieldService.Parse(record.CustomFieldsJson)
        });
    }

    [HttpPost("{studentId:int}/health-visits")]
    public async Task<ActionResult<StudentHealthVisitDto>> CreateHealthVisit(
        int studentId,
        [FromBody] CreateStudentHealthVisitDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(candidate => candidate.Id == studentId);
        if (student == null)
        {
            return BadRequest(new { Message = "The selected student ID is invalid." });
        }

        var (customError, customJson) = await BuildCustomFields("student-health-visits", model.CustomFields);
        if (customError != null) return customError;

        var recordedByName = await CurrentUserDisplayName();
        var visit = new StudentHealthVisit
        {
            StudentProfileId = student.Id,
            SISUserId = student.SISUserId,
            ExamTime = DateTime.SpecifyKind(model.VisitDate!.Value, DateTimeKind.Utc),
            FullName = student.FullName,
            Gender = student.Gender,
            ClassName = student.Homeroom,
            Symptoms = model.Symptoms.Trim(),
            Diagnosis = Clean(model.Diagnosis),
            Classification = Clean(model.Classification),
            Treatment = Clean(model.Treatment),
            Medication = Clean(model.Medication),
            ExaminedBy = Clean(model.ExaminedBy),
            CustomFieldsJson = customJson
        };

        var history = CreateManualImportHistory("student-health-visits", recordedByName);
        visit.ImportHistory = history;
        _context.StudentHealthVisits.Add(visit);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/students/{student.Id}", ToHealthVisitDto(visit));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentProfileDetailDto>> GetStudent(int id, [FromQuery] string? academicYear)
    {
        var hasYearFilter = !string.IsNullOrWhiteSpace(academicYear) && academicYear != "all";
        var student = await VisibleStudents()
            .AsSplitQuery()
            .Include(candidate => candidate.Behaviours)
            .Include(candidate => candidate.Projects)
            .Include(candidate => candidate.Parents)
            .Include(candidate => candidate.MentalHealthRecords)
            .Include(candidate => candidate.BusAttendanceRecords)
            .Include(candidate => candidate.HealthVisits)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (student == null)
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        // Behaviour and bus signals use their configured recent windows. Canvas risk uses
        // the current semester (or the active semester in the selected school year).
        var settings = await ClassificationSettings();
        var riskSince = now.AddDays(-settings.RiskWindowDays);
        var commendationSince = now.AddDays(-settings.CommendationWindowDays);
        var canvasPeriod = await _canvasReportingPeriods.ResolveAsync(
            hasYearFilter ? academicYear : null);
        var canvasAverage = (await CanvasAverages([student.Id], canvasPeriod)).GetValueOrDefault(student.Id);
        var risk = StudentClassificationService.ClassifyRisk(
            new StudentSignalSummary(
                student.Behaviours.Count(behaviour => behaviour.IncidentDate >= riskSince
                    && StudentClassificationService.IsConcerningBehaviour(behaviour)),
                student.Behaviours.Count(behaviour => behaviour.IncidentDate >= commendationSince
                    && StudentClassificationService.IsCommendationBehaviour(behaviour)),
                canvasAverage,
                student.BusAttendanceRecords.Count(record => record.AttendanceDate >= riskSince),
                student.BusAttendanceRecords.Count(record => record.AttendanceDate >= commendationSince)),
            settings);

        // Behaviours and projects re-scope to the selected school year (records carry an
        // academic year; older ones fall back to their date within the September-based range).
        var behaviourItems = FilterByAcademicYear(student.Behaviours, hasYearFilter, academicYear,
            behaviour => behaviour.AcademicYear, behaviour => behaviour.IncidentDate);
        var projectItems = FilterByAcademicYear(student.Projects, hasYearFilter, academicYear,
            project => project.AcademicYear, project => project.ActivityDate ?? project.CreatedAt);
        var busAttendanceItems = FilterByAcademicYear(
            student.BusAttendanceRecords, hasYearFilter, academicYear,
            record => record.AcademicYear, record => record.AttendanceDate);

        var detail = new StudentProfileDetailDto
        {
            Id = student.Id,
            SISStudentCode = student.SISStudentCode,
            SISUserId = student.SISUserId,
            FullName = student.FullName,
            ForeignName = student.ForeignName,
            Email = student.Email,
            PhoneNumber = student.PhoneNumber,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
            Homeroom = student.Homeroom,
            HomeroomCode = student.HomeroomCode,
            SISStatus = student.SISStatus,
            Campus = student.Campus,
            EducationLevel = student.EducationLevel,
            Program = student.Program,
            RiskLevel = risk.Level,
            Flags = risk.Flags,
            Summary = new StudentSummaryDto
            {
                TotalBehaviours = behaviourItems.Count,
                PositiveBehaviours = behaviourItems.Count(StudentClassificationService.IsCommendationBehaviour),
                ConcerningBehaviours = behaviourItems.Count(StudentClassificationService.IsConcerningBehaviour),
                BehavioursLast30Days = behaviourItems.Count(behaviour => behaviour.IncidentDate >= last30Days),
                TotalProjects = projectItems.Count,
                ActiveProjects = projectItems.Count(project =>
                    !StudentDataEntryOptions.IsCompletedProjectStatus(project.Status))
            },
            BehaviourTrend = BuildStudentBehaviourTrend(behaviourItems, hasYearFilter ? academicYear : null, now),
            Behaviours = behaviourItems
                .OrderByDescending(behaviour => behaviour.IncidentDate)
                .Select(behaviour => new StudentBehaviourDto
                {
                    Id = behaviour.Id,
                    IncidentDate = behaviour.IncidentDate,
                    BehaviourType = StudentClassificationService.CanonicalBehaviourTypeOrOriginal(behaviour.BehaviourType),
                    Category = StudentClassificationService.BehaviourCategory(behaviour.BehaviourType),
                    Notes = behaviour.Notes,
                    RecordedByName = behaviour.RecordedByName,
                    AcademicYear = behaviour.AcademicYear, Term = behaviour.Term,
                    MeasurementCycle = behaviour.MeasurementCycle, Program = behaviour.Program, Subject = behaviour.Subject,
                    CustomFields = ImportCustomFieldService.Parse(behaviour.CustomFieldsJson)
                })
                .ToList(),
            Projects = projectItems
                .OrderByDescending(project => project.CreatedAt)
                .Select(project => new StudentProjectDto
                {
                    Id = project.Id,
                    ProjectName = project.ProjectName,
                    Subject = project.Subject,
                    Status = project.Status,
                    TeacherNotes = project.TeacherNotes,
                    CreatedAt = project.CreatedAt,
                    AcademicYear = project.AcademicYear, Term = project.Term,
                    MeasurementCycle = project.MeasurementCycle, Program = project.Program, ActivityDate = project.ActivityDate,
                    CustomFields = ImportCustomFieldService.Parse(project.CustomFieldsJson)
                })
                .ToList(),
            Parents = student.Parents
                .OrderBy(parent => parent.Relationship)
                .ThenBy(parent => parent.FullName)
                .Select(parent => new StudentParentDto
                {
                    Id = parent.Id,
                    FullName = parent.FullName,
                    Relationship = parent.Relationship,
                    Username = parent.Username,
                    PhoneNumber = parent.PhoneNumber,
                    Email = parent.Email,
                    BirthYear = parent.BirthYear,
                    AgeGroup = parent.AgeGroup,
                    Occupation = parent.Occupation,
                    EmploymentSector = parent.EmploymentSector
                })
                .ToList(),
            MentalHealthRecords = student.MentalHealthRecords
                .Where(record => !hasYearFilter || DashboardPeriodNormalizer.AcademicYear(record.AcademicYear)
                    == academicYear)
                .OrderByDescending(record => record.RecordDate)
                .Select(ToMentalHealthDto)
                .ToList(),
            BusAttendanceRecords = busAttendanceItems
                .OrderByDescending(record => record.AttendanceDate)
                .Select(ToBusAttendanceDto)
                .ToList(),
            HealthVisits = FilterByAcademicYear(student.HealthVisits, hasYearFilter, academicYear,
                    _ => null, visit => visit.ExamTime)
                .OrderByDescending(visit => visit.ExamTime)
                .Select(ToHealthVisitDto)
                .ToList(),
            Canvas = await BuildCanvasSummary(student.Id,
                hasYearFilter ? academicYear : null)
        };

        return Ok(detail);
    }

    private static List<StudentBehaviourTrendPointDto> BuildStudentBehaviourTrend(
        IReadOnlyCollection<StudentBehaviour> behaviours,
        string? academicYear,
        DateTime now)
    {
        DateTime start;
        var monthCount = 6;
        if (!string.IsNullOrWhiteSpace(academicYear)
            && int.TryParse(academicYear.Split('-')[0], out var schoolYearStart))
        {
            start = new DateTime(schoolYearStart, DashboardPeriodNormalizer.SchoolYearStartMonth, 1);
            monthCount = 12;
        }
        else
        {
            start = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
        }

        var totals = behaviours
            .GroupBy(behaviour => new { behaviour.IncidentDate.Year, behaviour.IncidentDate.Month })
            .ToDictionary(group => (group.Key.Year, group.Key.Month), group => group.Count());

        return Enumerable.Range(0, monthCount)
            .Select(offset => start.AddMonths(offset))
            .Select(month => new StudentBehaviourTrendPointDto
            {
                Year = month.Year,
                Month = month.Month,
                Total = totals.GetValueOrDefault((month.Year, month.Month))
            })
            .ToList();
    }

    [HttpGet("{id:int}/canvas/course-grades")]
    public async Task<ActionResult<StudentCanvasCoursesPageDto>> GetCanvasCourseGrades(
        int id,
        [FromQuery] string? academicYear,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!await VisibleStudents().AnyAsync(student => student.Id == id))
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var year = await ResolveCanvasYear(id, academicYear);
        var period = await _canvasReportingPeriods.ResolveAsync(year, term);
        return Ok(await LoadCanvasCourseGrades(id, period, page, pageSize));
    }

    [HttpGet("{id:int}/canvas/summary")]
    public async Task<ActionResult<StudentCanvasDto>> GetCanvasSummary(
        int id,
        [FromQuery] string? academicYear,
        [FromQuery] string? term)
    {
        if (!await VisibleStudents().AnyAsync(student => student.Id == id))
        {
            return NotFound(new { Message = "Student was not found." });
        }

        return Ok(await BuildCanvasSummary(id, academicYear, term));
    }

    [HttpGet("{id:int}/canvas/recent-submissions")]
    public async Task<ActionResult<StudentCanvasSubmissionsPageDto>> GetCanvasRecentSubmissions(
        int id,
        [FromQuery] string? academicYear,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (!await VisibleStudents().AnyAsync(student => student.Id == id))
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var year = await ResolveCanvasYear(id, academicYear);
        var period = await _canvasReportingPeriods.ResolveAsync(year, term);
        return Ok(await LoadCanvasRecentSubmissions(id, period, page, pageSize));
    }

    [HttpGet("{id:int}/canvas/submissions")]
    public async Task<ActionResult<StudentCanvasSubmissionsPageDto>> GetCanvasSubmissions(
        int id,
        [FromQuery] string? category,
        [FromQuery] string? academicYear,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await VisibleStudents().AnyAsync(student => student.Id == id))
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var normalizedCategory = category?.Trim().ToLowerInvariant();
        if (normalizedCategory is not ("graded" or "submitted" or "missing" or "late"))
        {
            return BadRequest(new { Message = "Category must be graded, submitted, missing, or late." });
        }

        var year = await ResolveCanvasYear(id, academicYear);
        var period = await _canvasReportingPeriods.ResolveAsync(year, term);
        return Ok(await LoadCanvasSubmissions(id, period, normalizedCategory!, page, pageSize));
    }

    private async Task<StudentCanvasDto> BuildCanvasSummary(
        int studentProfileId, string? focusYear = null, string? focusTerm = null)
    {
        var scoreQuery = _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId == studentProfileId);
        var submissionQuery = _context.CanvasSubmissions.AsNoTracking()
            .Where(submission => submission.StudentProfileId == studentProfileId);
        if (!await scoreQuery.AnyAsync() && !await submissionQuery.AnyAsync())
        {
            return new StudentCanvasDto { HasData = false };
        }

        // Headline tiles/courses reflect one focus year (the selected year, or the latest on
        // record by default); the journey below always spans every year.
        var academicYears = await LoadCanvasAcademicYears(studentProfileId);
        var year = ResolveCanvasYear(focusYear, academicYears);
        var yearAliases = year is null ? [] : await CanvasAcademicYearAliases(year);
        var yearScores = year is null
            ? scoreQuery.Where(_ => false)
            : FilterCanvasScoresByAcademicYear(scoreQuery, year, yearAliases);
        var termLabels = year is null
            ? []
            : await FilterCanvasScoresByAcademicYear(
                    _context.CanvasCourseScores.AsNoTracking(), year, yearAliases)
                .Where(score => score.GradingPeriodTitle != null)
                .Select(score => score.GradingPeriodTitle!)
                .Distinct()
                .ToListAsync();
        var availableTerms = termLabels
            .Select(DashboardPeriodNormalizer.Term)
            .Where(term => term is "hk1" or "hk2")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(term => term == "hk1" ? 1 : 2)
            .ToList();
        var period = year is null ? null : await _canvasReportingPeriods.ResolveAsync(year, focusTerm);
        if (period is not null && !availableTerms.Contains(period.Term, StringComparer.OrdinalIgnoreCase))
        {
            availableTerms.Add(period.Term);
            availableTerms = availableTerms.OrderBy(term => term == "hk1" ? 1 : 2).ToList();
        }
        var latestScores = period is null
            ? scoreQuery.Where(_ => false)
            : CanvasReportingPeriodService.FilterScores(scoreQuery, period);
        var latestSubmissions = period is null
            ? submissionQuery.Where(_ => false)
            : CanvasReportingPeriodService.FilterSubmissions(submissionQuery, period);
        var gradedSubmissions = latestSubmissions.Where(submission => submission.WorkflowState == "graded");

        var enrolledCourses = await yearScores
            .Select(score => score.Enrollment!.CanvasCourseId)
            .Distinct()
            .CountAsync();
        var averageCourseGrade = await CanvasReportingPeriodService.FilterUsableScores(latestScores)
            .Select(score => score.FinalScore)
            .AverageAsync();
        var averageAssignmentScore = await gradedSubmissions
            .Where(submission => submission.Score != null && submission.Assignment!.PointsPossible > 0)
            .Select(submission => (double?)(submission.Score!.Value /
                submission.Assignment!.PointsPossible!.Value * 100))
            .AverageAsync();
        var assignmentsSubmitted = await latestSubmissions.CountAsync(submission => submission.SubmittedAt != null);
        var assignmentsGraded = await gradedSubmissions.CountAsync();
        var missingAssignments = await latestSubmissions.CountAsync(submission =>
            submission.Missing && submission.Excused != true);
        var lateSubmissions = await latestSubmissions.CountAsync(submission => submission.Late);

        // Grade trajectory: a semester average for HK1 and HK2 in each academic year.
        // Whole-course/full-year facts are deliberately excluded because they duplicate the
        // semester journey. Historical grade level is not inferred because neither Canvas nor
        // SIS supplied it for that historical period.
        var journeyRows = await CanvasReportingPeriodService.FilterUsableScores(scoreQuery)
            .Where(score => score.Enrollment!.Course!.EnrollmentTermName != null &&
                score.GradingPeriodTitle != null)
            .GroupBy(score => new
            {
                AcademicYear = score.Enrollment!.Course!.EnrollmentTermName,
                Term = score.GradingPeriodTitle,
                score.GradingPeriodEndDate
            })
            .Select(group => new
            {
                group.Key.AcademicYear,
                group.Key.Term,
                group.Key.GradingPeriodEndDate,
                AverageGrade = group.Average(score => score.FinalScore!.Value),
                CourseCount = group.Select(score => score.Enrollment!.CanvasCourseId).Distinct().Count(),
                RecordCount = group.Count()
            })
            .ToListAsync();
        var journey = journeyRows
            .Select(item =>
            {
                var academicYear = DashboardPeriodNormalizer.AcademicYear(
                    item.AcademicYear!, item.GradingPeriodEndDate);
                var normalizedTerm = DashboardPeriodNormalizer.Term(item.Term ?? "");
                return new
                {
                    AcademicYear = academicYear,
                    Term = normalizedTerm,
                    item.AverageGrade,
                    item.CourseCount,
                    item.RecordCount
                };
            })
            .Where(item => DashboardPeriodNormalizer.IsAcademicYear(item.AcademicYear) &&
                item.Term is "hk1" or "hk2")
            .GroupBy(item => new { item.AcademicYear, item.Term })
            .Select(group => new StudentCanvasJourneyPointDto
            {
                AcademicYear = group.Key.AcademicYear,
                Term = group.Key.Term,
                GradeLevel = null,
                Label = $"{group.Key.AcademicYear} {group.Key.Term}",
                AverageGrade = Math.Round((decimal)(group.Sum(item => item.AverageGrade * item.RecordCount) /
                    group.Sum(item => item.RecordCount)), 1),
                CourseCount = group.Sum(item => item.CourseCount)
            })
            .OrderBy(point => DashboardPeriodNormalizer.AcademicYearStart(point.AcademicYear))
            .ThenBy(point => point.Term == "hk1" ? 1 : 2)
            .ToList();

        var courseGradesPage = period is null
            ? new StudentCanvasCoursesPageDto { Page = 1, PageSize = 10 }
            : await LoadCanvasCourseGrades(studentProfileId, period, 1, 10);
        var submissionsPage = period is null
            ? new StudentCanvasSubmissionsPageDto { Page = 1, PageSize = 10 }
            : await LoadCanvasRecentSubmissions(studentProfileId, period, 1, 10);

        return new StudentCanvasDto
        {
            HasData = true,
            AcademicYear = year,
            Term = period?.Term,
            AvailableTerms = availableTerms,
            AcademicYears = academicYears,
            CoursesEnrolled = enrolledCourses,
            AverageCourseGrade = averageCourseGrade == null
                ? null : Math.Round((decimal)averageCourseGrade.Value, 1),
            AverageAssignmentScore = averageAssignmentScore == null
                ? null : Math.Round((decimal)averageAssignmentScore.Value, 1),
            AssignmentsSubmitted = assignmentsSubmitted,
            AssignmentsGraded = assignmentsGraded,
            MissingAssignments = missingAssignments,
            LateSubmissions = lateSubmissions,
            Journey = journey,
            Courses = courseGradesPage.Items,
            CourseGradesPage = courseGradesPage.Page,
            CourseGradesPageSize = courseGradesPage.PageSize,
            CourseGradesTotalCount = courseGradesPage.TotalCount,
            CourseGradesTotalPages = courseGradesPage.TotalPages,
            RecentSubmissions = submissionsPage.Items,
            RecentSubmissionsPage = submissionsPage.Page,
            RecentSubmissionsPageSize = submissionsPage.PageSize,
            RecentSubmissionsTotalCount = submissionsPage.TotalCount,
            RecentSubmissionsTotalPages = submissionsPage.TotalPages
        };
    }

    private async Task<string?> ResolveCanvasYear(int studentProfileId, string? requestedYear)
    {
        return ResolveCanvasYear(requestedYear, await LoadCanvasAcademicYears(studentProfileId));
    }

    private static string? ResolveCanvasYear(string? requestedYear, IReadOnlyList<string> academicYears)
    {
        var requested = string.IsNullOrWhiteSpace(requestedYear) || requestedYear == "all"
            ? null
            : DashboardPeriodNormalizer.AcademicYear(requestedYear.Trim());
        if (requested is not null && academicYears.Contains(requested, StringComparer.OrdinalIgnoreCase))
        {
            return requested;
        }

        return academicYears.FirstOrDefault();
    }

    private async Task<List<string>> LoadCanvasAcademicYears(int studentProfileId)
    {
        var scoreYears = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId == studentProfileId &&
                score.Enrollment!.Course!.EnrollmentTermName != null)
            .Select(score => new
            {
                Label = score.Enrollment!.Course!.EnrollmentTermName!,
                Date = score.GradingPeriodEndDate
            })
            .Distinct()
            .ToListAsync();
        var submissionYears = await _context.CanvasSubmissions.AsNoTracking()
            .Where(submission => submission.StudentProfileId == studentProfileId &&
                submission.Assignment!.Course!.EnrollmentTermName != null)
            .Select(submission => new
            {
                Label = submission.Assignment!.Course!.EnrollmentTermName!,
                Date = submission.CachedDueDate ?? submission.Assignment.DueAt ??
                    submission.GradedAt ?? submission.SubmittedAt
            })
            .Distinct()
            .ToListAsync();

        return scoreYears.Select(item => DashboardPeriodNormalizer.AcademicYear(item.Label, item.Date))
            .Concat(submissionYears.Select(item => DashboardPeriodNormalizer.AcademicYear(item.Label, item.Date)))
            .Where(DashboardPeriodNormalizer.IsAcademicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(DashboardPeriodNormalizer.AcademicYearStart)
            .ToList();
    }

    private async Task<StudentCanvasCoursesPageDto> LoadCanvasCourseGrades(
        int studentProfileId, CanvasReportingPeriod period, int page, int pageSize)
    {
        (page, pageSize) = NormalizeCanvasPaging(page, pageSize);
        var baseQuery = _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId == studentProfileId);
        var query = CanvasReportingPeriodService.FilterScores(baseQuery, period);
        var totalCount = await query.CountAsync();
        var totalPages = TotalPages(totalCount, pageSize);
        page = ClampPage(page, totalPages);
        var items = await query
            .OrderBy(score => score.Enrollment!.Course!.Name)
            .ThenBy(score => score.GradingPeriodId == null ? 0 : 1)
            .ThenBy(score => score.GradingPeriodStartDate)
            .ThenBy(score => score.GradingPeriodId)
            .ThenBy(score => score.WarehouseScoreFactId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(score => new StudentCanvasCourseDto
            {
                CanvasCourseId = score.Enrollment!.CanvasCourseId,
                CourseName = score.Enrollment!.Course!.Name ?? "—",
                CourseCode = score.Enrollment.Course.CourseCode,
                EnrollmentTermId = score.Enrollment.Course.EnrollmentTermId,
                EnrollmentTermName = score.Enrollment.Course.EnrollmentTermName,
                GradingPeriodId = score.GradingPeriodId,
                GradingPeriodTitle = score.GradingPeriodTitle,
                ScoreScope = score.GradingPeriodId == null ? "course_total" : "grading_period",
                CurrentScore = score.CurrentScore == null ||
                    score.CurrentScore == 0 && (score.CurrentPoints == null || score.CurrentPoints == 0)
                    && (score.CurrentGrade == null || score.CurrentGrade == "")
                    && (score.FinalGrade == null || score.FinalGrade == "")
                    ? null : (decimal)score.CurrentScore.Value,
                FinalScore = score.FinalScore == null ||
                    score.FinalScore == 0 && (score.CurrentPoints == null || score.CurrentPoints == 0)
                    && (score.CurrentGrade == null || score.CurrentGrade == "")
                    && (score.FinalGrade == null || score.FinalGrade == "")
                    ? null : (decimal)score.FinalScore.Value
            })
            .ToListAsync();

        return new StudentCanvasCoursesPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private async Task<StudentCanvasSubmissionsPageDto> LoadCanvasRecentSubmissions(
        int studentProfileId, CanvasReportingPeriod period, int page, int pageSize)
    {
        (page, pageSize) = NormalizeCanvasPaging(page, pageSize);
        var baseQuery = _context.CanvasSubmissions.AsNoTracking()
            .Where(submission => submission.StudentProfileId == studentProfileId);
        var query = CanvasReportingPeriodService.FilterSubmissions(baseQuery, period);
        var totalCount = await query.CountAsync();
        var totalPages = TotalPages(totalCount, pageSize);
        page = ClampPage(page, totalPages);
        var items = await query
            .OrderByDescending(submission =>
                submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.SubmittedAt)
            .ThenByDescending(submission => submission.CanvasSubmissionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(submission => new StudentCanvasSubmissionDto
            {
                CanvasSubmissionId = submission.CanvasSubmissionId,
                CanvasAssignmentId = submission.CanvasAssignmentId,
                CanvasCourseId = submission.CanvasCourseId,
                AssignmentTitle = submission.Assignment!.Name ?? "—",
                CourseName = submission.Assignment.Course!.Name,
                Subject = submission.Assignment.Course.Name,
                WorkflowState = submission.WorkflowState ?? "",
                Grade = submission.Grade,
                SubmissionType = submission.SubmissionType,
                Score = submission.Score == null
                    ? null : Math.Round((decimal)submission.Score.Value, 1),
                PointsPossible = submission.Assignment.PointsPossible == null
                    ? null : Math.Round((decimal)submission.Assignment.PointsPossible.Value, 1),
                DueAt = submission.CachedDueDate ?? submission.Assignment.DueAt,
                SubmittedAt = submission.SubmittedAt,
                GradedAt = submission.GradedAt,
                Late = submission.Late,
                Missing = submission.Missing,
                Excused = submission.Excused
            })
            .ToListAsync();

        return new StudentCanvasSubmissionsPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private async Task<StudentCanvasSubmissionsPageDto> LoadCanvasSubmissions(
        int studentProfileId, CanvasReportingPeriod period, string category, int page, int pageSize)
    {
        (page, pageSize) = NormalizeCanvasPaging(page, pageSize);
        var baseQuery = _context.CanvasSubmissions.AsNoTracking()
            .Where(submission => submission.StudentProfileId == studentProfileId);
        var query = CanvasReportingPeriodService.FilterSubmissions(baseQuery, period);
        query = category switch
        {
            "graded" => query.Where(submission => submission.WorkflowState == "graded"),
            "submitted" => query.Where(submission => submission.SubmittedAt != null),
            "missing" => query.Where(submission => submission.Missing && submission.Excused != true),
            "late" => query.Where(submission => submission.Late),
            _ => query.Where(_ => false)
        };

        var totalCount = await query.CountAsync();
        var totalPages = TotalPages(totalCount, pageSize);
        page = ClampPage(page, totalPages);
        var items = await query
            .OrderByDescending(submission => submission.GradedAt
                ?? submission.SubmittedAt
                ?? submission.CachedDueDate
                ?? submission.Assignment!.DueAt)
            .ThenByDescending(submission => submission.CanvasSubmissionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(submission => new StudentCanvasSubmissionDto
            {
                CanvasSubmissionId = submission.CanvasSubmissionId,
                CanvasAssignmentId = submission.CanvasAssignmentId,
                CanvasCourseId = submission.CanvasCourseId,
                AssignmentTitle = submission.Assignment!.Name ?? "—",
                CourseName = submission.Assignment.Course!.Name,
                Subject = submission.Assignment.Course.Name,
                WorkflowState = submission.WorkflowState ?? "",
                Grade = submission.Grade,
                SubmissionType = submission.SubmissionType,
                Score = submission.Score == null
                    ? null : Math.Round((decimal)submission.Score.Value, 1),
                PointsPossible = submission.Assignment.PointsPossible == null
                    ? null : Math.Round((decimal)submission.Assignment.PointsPossible.Value, 1),
                DueAt = submission.CachedDueDate ?? submission.Assignment.DueAt,
                SubmittedAt = submission.SubmittedAt,
                GradedAt = submission.GradedAt,
                Late = submission.Late,
                Missing = submission.Missing,
                Excused = submission.Excused
            })
            .ToListAsync();

        return new StudentCanvasSubmissionsPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private async Task<string[]> CanvasAcademicYearAliases(string academicYear)
    {
        var labels = await _context.CanvasCourses.AsNoTracking()
            .Where(course => course.EnrollmentTermName != null && course.EnrollmentTermName != "")
            .Select(course => course.EnrollmentTermName!)
            .Distinct()
            .ToListAsync();
        return labels.Where(label => DashboardPeriodNormalizer.AcademicYear(label)
                .Equals(academicYear, StringComparison.OrdinalIgnoreCase))
            .Append(academicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IQueryable<CanvasCourseScore> FilterCanvasScoresByAcademicYear(
        IQueryable<CanvasCourseScore> query, string academicYear, IReadOnlyCollection<string> aliases)
    {
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        return query.WhereMatchesAny(score => score.Enrollment!.Course!.EnrollmentTermName,
            aliases, score => score.GradingPeriodEndDate >= start && score.GradingPeriodEndDate < end);
    }

    private static IQueryable<CanvasSubmission> FilterCanvasSubmissionsByAcademicYear(
        IQueryable<CanvasSubmission> query, string academicYear, IReadOnlyCollection<string> aliases)
    {
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        return query.WhereMatchesAny(submission => submission.Assignment!.Course!.EnrollmentTermName,
                aliases, submission =>
                    (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt ??
                        submission.SubmittedAt) >= start &&
                    (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt ??
                        submission.SubmittedAt) < end)
            .Where(submission =>
                (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt ??
                    submission.SubmittedAt) == null ||
                (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt ??
                    submission.SubmittedAt) >= start &&
                (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt ??
                    submission.SubmittedAt) < end);
    }

    private static (int Page, int PageSize) NormalizeCanvasPaging(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));

    private static int ClampPage(int page, int totalPages) =>
        totalPages == 0 ? 1 : Math.Min(page, totalPages);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLikePattern(string value) =>
        value.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

    /// <summary>Validate and serialise admin-defined custom fields for a manual entry.</summary>
    private async Task<(ActionResult? Error, string? Json)> BuildCustomFields(
        string moduleId, Dictionary<string, string?>? values)
    {
        var fields = await _context.ImportModuleFields.AsNoTracking()
            .Where(f => f.ModuleId == moduleId)
            .OrderBy(f => f.SortOrder).ToListAsync();
        if (fields.Count == 0)
        {
            return (null, null);
        }
        var (errors, json) = ImportCustomFieldService.Build(fields, values);
        if (errors.Count > 0)
        {
            return (BadRequest(new { Message = string.Join(" ", errors), Errors = errors }), null);
        }
        return (null, json);
    }

    private static string? Canonical(IReadOnlyList<string> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));

    private static List<T> FilterByAcademicYear<T>(
        IEnumerable<T> items, bool apply, string? academicYear,
        Func<T, string?> yearSelector, Func<T, DateTime> dateSelector)
    {
        if (!apply || string.IsNullOrWhiteSpace(academicYear))
        {
            return items.ToList();
        }
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        return items.Where(item =>
            (!string.IsNullOrWhiteSpace(yearSelector(item)) &&
                DashboardPeriodNormalizer.AcademicYear(yearSelector(item)!) == academicYear)
            || (yearSelector(item) == null && dateSelector(item) >= start && dateSelector(item) < end))
            .ToList();
    }

    private static StudentMentalHealthRecordDto ToMentalHealthDto(StudentMentalHealthRecord record) => new()
    {
        Id = record.Id,
        RecordDate = record.RecordDate,
        AcademicYear = record.AcademicYear,
        ConcernType = record.ConcernType,
        RiskLevel = record.RiskLevel,
        Notes = record.Notes,
        RecordedByName = record.RecordedByName,
        CustomFields = ImportCustomFieldService.Parse(record.CustomFieldsJson)
    };

    private static StudentHealthVisitDto ToHealthVisitDto(StudentHealthVisit visit) => new()
    {
        Id = visit.Id,
        StudentProfileId = visit.StudentProfileId,
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
        ExaminedBy = visit.ExaminedBy,
        CustomFields = ImportCustomFieldService.Parse(visit.CustomFieldsJson)
    };

    private static StudentBusAttendanceRecordDto ToBusAttendanceDto(StudentBusAttendanceRecord record) => new()
    {
        Id = record.Id,
        AttendanceDate = record.AttendanceDate,
        TripDirection = record.TripDirection,
        ViolationType = record.ViolationType,
        BusCode = record.BusCode,
        RouteName = record.RouteName,
        Notes = record.Notes,
        RecordedByName = record.RecordedByName,
        AcademicYear = record.AcademicYear,
        Term = record.Term,
        CustomFields = ImportCustomFieldService.Parse(record.CustomFieldsJson)
    };

    private async Task<List<StudentProfile>> LoadVisibleWithActivity(
        string? search = null, string? grade = null)
    {
        var query = VisibleStudents();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(student =>
                EF.Functions.Like(student.FullName, pattern, "!") ||
                EF.Functions.Like(student.SISUserId, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!") ||
                student.GradeLevel != null && EF.Functions.Like(student.GradeLevel, pattern, "!") ||
                student.CanvasGradeLevel != null && EF.Functions.Like(student.CanvasGradeLevel, pattern, "!"));
        }
        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = grade == "—"
                ? query.Where(student => (student.GradeLevel ?? student.CanvasGradeLevel) == null ||
                    (student.GradeLevel ?? student.CanvasGradeLevel) == "")
                : query.Where(student => (student.GradeLevel ?? student.CanvasGradeLevel) == grade);
        }

        return await query
            .AsSplitQuery()
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .Include(student => student.MentalHealthRecords)
            .Include(student => student.BusAttendanceRecords)
            .ToListAsync();
    }

    private async Task<List<StudentSignalRow>> LoadVisibleSignals(
        StudentClassificationSettingsDto settings,
        CanvasReportingPeriod canvasPeriod,
        string? search = null,
        string? grade = null)
    {
        var students = await LoadVisibleWithActivity(search, grade);
        var now = DateTime.UtcNow;
        var riskSince = now.AddDays(-settings.RiskWindowDays);
        var commendationSince = now.AddDays(-settings.CommendationWindowDays);
        var studentIds = students.Select(student => student.Id).ToList();
        var scoreQuery = _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId != null
                && studentIds.Contains(score.StudentProfileId.Value));
        var periodScores = CanvasReportingPeriodService.FilterScores(scoreQuery, canvasPeriod);
        var scoreRows = await CanvasReportingPeriodService.FilterUsableScores(periodScores)
            .Select(score => new
            {
                StudentId = score.StudentProfileId!.Value,
                Value = (decimal)score.FinalScore!.Value,
                Date = score.UpdatedAt ?? score.CreatedAt
            })
            .ToListAsync();
        var canvasByStudent = scoreRows
            .GroupBy(score => score.StudentId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Average = Math.Round(group.Average(score => score.Value), 1),
                    Latest = group.Max(score => score.Date)
                });

        return students.Select(student =>
        {
            var canvas = canvasByStudent.GetValueOrDefault(student.Id);
            return new StudentSignalRow(
                student,
                new StudentSignalSummary(
                    student.Behaviours.Count(behaviour => behaviour.IncidentDate >= riskSince
                        && StudentClassificationService.IsConcerningBehaviour(behaviour)),
                    student.Behaviours.Count(behaviour => behaviour.IncidentDate >= commendationSince
                        && StudentClassificationService.IsCommendationBehaviour(behaviour)),
                    canvas?.Average,
                    student.BusAttendanceRecords.Count(record => record.AttendanceDate >= riskSince),
                    student.BusAttendanceRecords.Count(record => record.AttendanceDate >= commendationSince)),
                canvas?.Latest);
        }).ToList();
    }

    private async Task<Dictionary<int, decimal>> CanvasAverages(
        IReadOnlyCollection<int> studentIds,
        CanvasReportingPeriod canvasPeriod)
    {
        if (studentIds.Count == 0) return [];
        var scoreQuery = _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId != null
                && studentIds.Contains(score.StudentProfileId.Value));
        var periodScores = CanvasReportingPeriodService.FilterScores(scoreQuery, canvasPeriod);
        var scores = await CanvasReportingPeriodService.FilterUsableScores(periodScores)
            .Select(score => new
            {
                StudentId = score.StudentProfileId!.Value,
                Value = (decimal)score.FinalScore!.Value
            })
            .ToListAsync();
        return scores.GroupBy(score => score.StudentId)
            .ToDictionary(group => group.Key, group => Math.Round(group.Average(score => score.Value), 1));
    }

    private async Task<StudentClassificationSettingsDto> ClassificationSettings()
    {
        var setting = await _context.StudentClassificationSettings.AsNoTracking()
            .OrderBy(item => item.Id).FirstOrDefaultAsync();
        return setting == null ? new StudentClassificationSettingsDto() : ToSettingsDto(setting);
    }

    private static StudentClassificationSettingsDto ToSettingsDto(StudentClassificationSetting setting) => new()
    {
        RiskWindowDays = setting.RiskWindowDays,
        HighRiskBehaviourCount = setting.HighRiskBehaviourCount,
        HighRiskBusViolationCount = setting.HighRiskBusViolationCount,
        HighRiskCanvasAverageBelow = setting.HighRiskCanvasAverageBelow,
        CommendationWindowDays = setting.CommendationWindowDays,
        CommendationPositiveBehaviourCount = setting.CommendationPositiveBehaviourCount,
        CommendationCanvasAverageAtLeast = setting.CommendationCanvasAverageAtLeast,
        CommendationMaxBusViolations = setting.CommendationMaxBusViolations
    };

    private void ValidateClassificationSettings(StudentClassificationSettingsDto model)
    {
        if (model.RiskWindowDays is < 1 or > 365)
            ModelState.AddModelError(nameof(model.RiskWindowDays), "Must be between 1 and 365 days.");
        if (model.CommendationWindowDays is < 1 or > 365)
            ModelState.AddModelError(nameof(model.CommendationWindowDays), "Must be between 1 and 365 days.");
        if (model.HighRiskBehaviourCount is < 0 or > 1000)
            ModelState.AddModelError(nameof(model.HighRiskBehaviourCount), "Must be between 0 and 1000.");
        if (model.HighRiskBusViolationCount is < 0 or > 1000)
            ModelState.AddModelError(nameof(model.HighRiskBusViolationCount), "Must be between 0 and 1000.");
        if (model.CommendationPositiveBehaviourCount is < 1 or > 1000)
            ModelState.AddModelError(nameof(model.CommendationPositiveBehaviourCount), "Must be between 1 and 1000.");
        if (model.CommendationMaxBusViolations is < 0 or > 1000)
            ModelState.AddModelError(nameof(model.CommendationMaxBusViolations), "Must be between 0 and 1000.");
        if (model.HighRiskCanvasAverageBelow is < 0 or > 100)
            ModelState.AddModelError(nameof(model.HighRiskCanvasAverageBelow), "Must be between 0 and 100.");
        if (model.CommendationCanvasAverageAtLeast is < 0 or > 100)
            ModelState.AddModelError(nameof(model.CommendationCanvasAverageAtLeast), "Must be between 0 and 100.");
    }

    private IQueryable<StudentProfile> VisibleStudents()
        => _context.StudentProfiles.AsNoTracking();

    private async Task<string> CurrentUserDisplayName()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var fullName = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _context.Users
                .Where(user => user.Id == userId)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(fullName) ? User.Identity?.Name ?? "Unknown" : fullName;
    }

    private ImportHistory CreateManualImportHistory(string moduleId, string importedByName)
    {
        return new ImportHistory
        {
            ModuleId = moduleId,
            FileName = moduleId switch
            {
                "student-behaviours" => "Hành vi học sinh",
                "student-projects" => "Dự án học sinh",
                "student-mental-health" => "Tâm lý học sinh",
                "student-bus-attendance" => "Điểm danh xe bus",
                _ => moduleId
            },
            Status = "Valid",
            ValidRows = 1,
            InvalidRows = 0,
            ImportedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ImportedByUsername = User.Identity?.Name ?? "unknown",
            ImportedByName = importedByName
        };
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static int TotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

}
