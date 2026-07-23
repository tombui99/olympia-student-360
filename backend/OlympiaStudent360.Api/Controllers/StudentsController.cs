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

    public StudentsController(AppDbContext context)
    {
        _context = context;
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
            .Where(student => student.GradeLevel != null && student.GradeLevel != "")
            .Select(student => student.GradeLevel!)
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
                EF.Functions.Like(student.StudentCode, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!") ||
                student.SISClassMemberships.Any(membership => membership.SISClass.Teachers.Any(
                    assignment => EF.Functions.Like(assignment.Teacher.FullName, pattern, "!"))));
        }
        if (!string.IsNullOrWhiteSpace(grade))
        {
            query = query.Where(student => student.GradeLevel == grade.Trim());
        }

        var settings = await ClassificationSettings();
        var riskSince = DateTime.UtcNow.AddDays(-settings.RiskWindowDays);
        if (atRiskOnly)
        {
            var atRiskIds = (await LoadVisibleSignals(settings))
                .Where(entry => StudentClassificationService.ClassifyRisk(entry.Signals, settings).Level != "None")
                .Select(entry => entry.Student.Id)
                .ToList();
            query = query.Where(student => atRiskIds.Contains(student.Id));
        }

        var rows = query.Select(student => new
        {
            student.Id,
            student.StudentCode,
            student.FullName,
            student.GradeLevel,
            student.Homeroom,
            student.Campus,
            student.EducationLevel,
            student.Program,
            TeacherName = student.SISClassMemberships.SelectMany(membership => membership.SISClass.Teachers)
                .Select(assignment => assignment.Teacher.FullName).FirstOrDefault(),
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
            .ThenBy(student => student.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var pageStudentIds = pageRows.Select(student => student.Id).ToList();
        var canvasAverages = await CanvasAverages(pageStudentIds);
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
                    Id = student.Id, StudentCode = student.StudentCode, FullName = student.FullName,
                    GradeLevel = student.GradeLevel, Homeroom = student.Homeroom,
                    Campus = student.Campus, EducationLevel = student.EducationLevel, Program = student.Program,
                    TeacherName = student.TeacherName, BehaviourCount = student.BehaviourCount,
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
        [FromQuery] int pageSize = 10)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var settings = await ClassificationSettings();
        var atRiskStudents = (await LoadVisibleSignals(settings))
            .Select(entry => new
            {
                entry.Student,
                entry.Signals,
                Risk = StudentClassificationService.ClassifyRisk(entry.Signals, settings)
            })
            .Where(entry => entry.Risk.Level != "None")
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
                    StudentCode = entry.Student.StudentCode,
                    FullName = entry.Student.FullName,
                    GradeLevel = entry.Student.GradeLevel,
                    Homeroom = entry.Student.Homeroom,
                    TeacherName = SISClassVisibility.TeacherNames(entry.Student),
                    BehaviourCount = entry.Student.Behaviours.Count,
                    PositiveBehaviourCount = entry.Student.Behaviours.Count(StudentClassificationService.IsCommendationBehaviour),
                    ConcerningBehaviourCount = entry.Student.Behaviours.Count(StudentClassificationService.IsConcerningBehaviour),
                    ProjectCount = entry.Student.Projects.Count,
                    MentalHealthCount = entry.Student.MentalHealthRecords.Count,
                    RiskLevel = entry.Risk.Level,
                    RiskFlagCount = entry.Risk.Flags.Count,
                    Flags = entry.Risk.Flags
                })
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = TotalPages(totalCount, pageSize),
            HighRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "High"),
            MediumRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "Medium")
        });
    }

    [HttpGet("classification-settings")]
    public async Task<ActionResult<StudentClassificationSettingsDto>> GetClassificationSettings() =>
        Ok(await ClassificationSettings());

    [HttpPut("classification-settings")]
    [Authorize(Roles = "Admin,Principal")]
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
        [FromQuery] string? grade = null)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var settings = await ClassificationSettings();
        var commendationSince = DateTime.UtcNow.AddDays(-settings.CommendationWindowDays);
        var commendations = (await LoadVisibleSignals(settings))
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
                StudentCode = entry.Student.StudentCode,
                StudentName = entry.Student.FullName,
                GradeLevel = entry.Student.GradeLevel,
                Homeroom = entry.Student.Homeroom,
                TeacherName = SISClassVisibility.TeacherNames(entry.Student),
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
             commendation.StudentCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
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
            AvailableGrades = availableGrades
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
                EF.Functions.Like(student.StudentCode, pattern, "!") ||
                EF.Functions.Like(student.FullName, pattern, "!") ||
                student.Homeroom != null && EF.Functions.Like(student.Homeroom, pattern, "!"))
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .Take(resultLimit)
            .Select(student => new ManualEntryStudentDto
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel,
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
    [Authorize(Roles = "Admin,Principal,Teacher")]
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

    [HttpGet("cohorts")]
    public async Task<ActionResult<CohortsResponseDto>> GetCohorts()
    {
        var settings = await ClassificationSettings();
        var evaluated = (await LoadVisibleSignals(settings))
            .Select(entry =>
            {
                var student = entry.Student;
                var risk = StudentClassificationService.ClassifyRisk(entry.Signals, settings);
                return new CohortEntry(
                    string.IsNullOrWhiteSpace(student.GradeLevel) ? "—" : student.GradeLevel!,
                    string.IsNullOrWhiteSpace(student.Homeroom) ? "—" : student.Homeroom!,
                    risk.Level,
                    student.Behaviours.Count,
                    student.Projects.Count);
            })
            .ToList();

        return Ok(new CohortsResponseDto
        {
            Grades = BuildCohorts(evaluated.GroupBy(entry => entry.Grade), key => $"Grade {key}"),
            Homerooms = BuildCohorts(evaluated.GroupBy(entry => entry.Homeroom), key => key)
        });
    }

    private record CohortEntry(
        string Grade,
        string Homeroom,
        string Level,
        int BehaviourCount,
        int ProjectCount);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentProfileDetailDto>> GetStudent(int id, [FromQuery] string? academicYear)
    {
        var hasYearFilter = !string.IsNullOrWhiteSpace(academicYear) && academicYear != "all";
        var student = await VisibleStudents()
            .AsSplitQuery()
            .Include(candidate => candidate.SISClassMemberships)
                .ThenInclude(membership => membership.SISClass)
                .ThenInclude(sisClass => sisClass.Teachers)
                .ThenInclude(assignment => assignment.Teacher)
            .Include(candidate => candidate.Behaviours)
            .Include(candidate => candidate.Projects)
            .Include(candidate => candidate.Parents)
            .Include(candidate => candidate.MentalHealthRecords)
            .Include(candidate => candidate.BusAttendanceRecords)
            .Include(candidate => candidate.SISHealthVisits)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (student == null)
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        // Risk always reflects the student's full record, not the selected year.
        var settings = await ClassificationSettings();
        var riskSince = now.AddDays(-settings.RiskWindowDays);
        var commendationSince = now.AddDays(-settings.CommendationWindowDays);
        var canvasAverage = (await CanvasAverages([student.Id])).GetValueOrDefault(student.Id);
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
        // academic year; older ones fall back to their date within the Aug–Jul range).
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
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            GradeLevel = student.GradeLevel,
            Homeroom = student.Homeroom,
            Campus = student.Campus,
            EducationLevel = student.EducationLevel,
            Program = student.Program,
            TeacherName = SISClassVisibility.TeacherNames(student),
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
                    PhoneNumber = parent.PhoneNumber,
                    Email = parent.Email,
                    AgeGroup = parent.AgeGroup,
                    Occupation = parent.Occupation,
                    EmploymentSector = parent.EmploymentSector
                })
                .ToList(),
            MentalHealthRecords = student.MentalHealthRecords
                .Where(record => !hasYearFilter || record.AcademicYear == academicYear)
                .OrderByDescending(record => record.RecordDate)
                .Select(ToMentalHealthDto)
                .ToList(),
            BusAttendanceRecords = busAttendanceItems
                .OrderByDescending(record => record.AttendanceDate)
                .Select(ToBusAttendanceDto)
                .ToList(),
            HealthVisits = FilterByAcademicYear(student.SISHealthVisits, hasYearFilter, academicYear,
                    _ => null, visit => visit.ExamTime)
                .OrderByDescending(visit => visit.ExamTime)
                .Select(ToSISHealthVisitDto)
                .ToList(),
            Canvas = await BuildCanvasSummary(student.Id, student.GradeLevel,
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
            start = new DateTime(schoolYearStart, 8, 1);
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

    private async Task<StudentCanvasDto> BuildCanvasSummary(
        int studentProfileId, string? currentGradeLevel, string? focusYear = null)
    {
        var courseScores = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId == studentProfileId && score.CourseScore)
            .Select(score => new
            {
                score.CourseName, score.Subject, score.Term, score.GradingPeriodTitle,
                score.CurrentScore, score.FinalScore, score.AcademicYear
            })
            .ToListAsync();

        var submissions = await _context.CanvasSubmissions.AsNoTracking()
            .Where(submission => submission.StudentProfileId == studentProfileId)
            .Select(submission => new
            {
                submission.WorkflowState, submission.Score, submission.PointsPossible,
                submission.Late, submission.Missing, submission.Excused,
                submission.DueAt, submission.SubmittedAt,
                submission.Subject, submission.CanvasAssignmentId, submission.AcademicYear
            })
            .ToListAsync();

        if (courseScores.Count == 0 && submissions.Count == 0)
        {
            return new StudentCanvasDto { HasData = false };
        }

        // Headline tiles/courses reflect one focus year (the selected year, or the latest on
        // record by default); the journey below always spans every year.
        var latestYear = courseScores.Select(item => item.AcademicYear)
            .Concat(submissions.Select(item => item.AcademicYear))
            .Where(year => year != null)
            .OrderByDescending(year => year)
            .FirstOrDefault();
        var yearOnRecord = focusYear != null
            && (courseScores.Any(item => item.AcademicYear == focusYear)
                || submissions.Any(item => item.AcademicYear == focusYear));
        var year = yearOnRecord ? focusYear : latestYear;

        var latestScores = courseScores.Where(item => item.AcademicYear == year).ToList();
        var latestSubmissions = submissions.Where(item => item.AcademicYear == year).ToList();

        var enrolledCourses = latestScores.Select(item => item.CourseName).Distinct().Count();
        var graded = latestSubmissions
            .Where(item => string.Equals(item.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var scoredPercentages = graded
            .Where(item => item.Score != null && item.PointsPossible is > 0)
            .Select(item => item.Score!.Value / item.PointsPossible!.Value * 100)
            .ToList();
        var finalScores = latestScores.Where(item => item.FinalScore != null)
            .Select(item => item.FinalScore!.Value).ToList();

        // Grade trajectory: average final grade per academic year + term, in chronological order.
        // Derive the grade level the student was in each year, assuming one grade per year with
        // their current grade anchored to the latest year — so the journey reads as a
        // primary-through-high-school arc (Grade 1 … Grade 12).
        int StartYear(string yr) => int.TryParse(yr.Split('-')[0], out var value) ? value : 0;
        var hasGrade = int.TryParse(currentGradeLevel, out var currentGrade);
        var latestStartYear = courseScores
            .Where(item => item.AcademicYear != null)
            .Select(item => StartYear(item.AcademicYear!))
            .DefaultIfEmpty(0)
            .Max();

        var journey = courseScores
            .Where(item => item.FinalScore != null && item.AcademicYear != null)
            .GroupBy(item => new { item.AcademicYear, item.Term })
            .Select(group =>
            {
                string? gradeThatYear = null;
                if (hasGrade)
                {
                    var derived = currentGrade - (latestStartYear - StartYear(group.Key.AcademicYear!));
                    if (derived is >= 1 and <= 12)
                    {
                        gradeThatYear = derived.ToString();
                    }
                }
                var label = gradeThatYear != null
                    ? $"G{gradeThatYear} {TermShort(group.Key.Term)}".Trim()
                    : $"{group.Key.AcademicYear} {TermShort(group.Key.Term)}".Trim();
                return new StudentCanvasJourneyPointDto
                {
                    AcademicYear = group.Key.AcademicYear!,
                    Term = group.Key.Term,
                    GradeLevel = gradeThatYear,
                    Label = label,
                    AverageGrade = Math.Round((decimal)group.Average(item => item.FinalScore!.Value), 1),
                    CourseCount = group.Select(item => item.CourseName).Distinct().Count()
                };
            })
            .OrderBy(point => point.AcademicYear)
            .ThenBy(point => point.Term)
            .ToList();

        // Join titles/course names for the recent-submissions list.
        var assignmentIds = submissions.Select(item => item.CanvasAssignmentId).Distinct().ToList();
        var assignments = await _context.CanvasAssignments.AsNoTracking()
            .Where(assignment => assignmentIds.Contains(assignment.CanvasAssignmentId))
            .Select(assignment => new { assignment.CanvasAssignmentId, assignment.Title, assignment.CanvasCourseId })
            .ToListAsync();
        var courseIds = assignments.Select(item => item.CanvasCourseId).Distinct().ToList();
        var courseNames = await _context.CanvasCourses.AsNoTracking()
            .Where(course => courseIds.Contains(course.CanvasCourseId))
            .ToDictionaryAsync(course => course.CanvasCourseId, course => course.Name);
        var assignmentLookup = assignments.ToDictionary(item => item.CanvasAssignmentId);

        return new StudentCanvasDto
        {
            HasData = true,
            AcademicYear = year,
            CoursesEnrolled = enrolledCourses,
            AverageCourseGrade = finalScores.Count == 0
                ? null : Math.Round((decimal)finalScores.Average(), 1),
            AverageAssignmentScore = scoredPercentages.Count == 0
                ? null : Math.Round((decimal)scoredPercentages.Average(), 1),
            AssignmentsSubmitted = latestSubmissions.Count(item => item.SubmittedAt != null),
            AssignmentsGraded = graded.Count,
            MissingAssignments = latestSubmissions.Count(item => item.Missing && !item.Excused),
            LateSubmissions = latestSubmissions.Count(item => item.Late),
            Journey = journey,
            Courses = latestScores
                .OrderBy(item => item.Subject).ThenBy(item => item.Term)
                .Select(item => new StudentCanvasCourseDto
                {
                    CourseName = item.CourseName ?? "—",
                    Subject = item.Subject,
                    Term = item.Term,
                    GradingPeriodTitle = item.GradingPeriodTitle,
                    CurrentScore = item.CurrentScore == null ? null : Math.Round((decimal)item.CurrentScore.Value, 1),
                    FinalScore = item.FinalScore == null ? null : Math.Round((decimal)item.FinalScore.Value, 1)
                })
                .ToList(),
            RecentSubmissions = submissions
                .OrderByDescending(item => item.DueAt ?? item.SubmittedAt ?? DateTime.MinValue)
                .Take(15)
                .Select(item => new StudentCanvasSubmissionDto
                {
                    AssignmentTitle = assignmentLookup.TryGetValue(item.CanvasAssignmentId, out var assignment)
                        ? assignment.Title : "—",
                    CourseName = assignmentLookup.TryGetValue(item.CanvasAssignmentId, out var course)
                        && courseNames.TryGetValue(course.CanvasCourseId, out var name) ? name : null,
                    Subject = item.Subject,
                    WorkflowState = item.WorkflowState,
                    Score = item.Score == null ? null : Math.Round((decimal)item.Score.Value, 1),
                    PointsPossible = item.PointsPossible == null ? null : Math.Round((decimal)item.PointsPossible.Value, 1),
                    DueAt = item.DueAt,
                    SubmittedAt = item.SubmittedAt,
                    Late = item.Late,
                    Missing = item.Missing,
                    Excused = item.Excused
                })
                .ToList()
        };
    }

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
        var (start, end) = AcademicYearRange(academicYear);
        return items.Where(item => yearSelector(item) == academicYear
            || (yearSelector(item) == null && dateSelector(item) >= start && dateSelector(item) < end))
            .ToList();
    }

    // School year runs August–July.
    private static (DateTime Start, DateTime End) AcademicYearRange(string academicYear)
    {
        var startYear = int.TryParse(academicYear.Split('-')[0], out var value) ? value : DateTime.UtcNow.Year;
        return (new DateTime(startYear, 8, 1), new DateTime(startYear + 1, 8, 1));
    }

    private static string TermShort(string? term) => term switch
    {
        "hk1" => "S1",
        "hk2" => "S2",
        null or "" => "",
        _ => term!
    };

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

    private static SISHealthVisitDto ToSISHealthVisitDto(SISHealthVisit visit) => new()
    {
        Id = visit.Id,
        StudentProfileId = visit.StudentProfileId,
        StudentCode = visit.Student?.StudentCode,
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

    private static List<CohortStatsDto> BuildCohorts(
        IEnumerable<IGrouping<string, CohortEntry>> groups,
        Func<string, string> labelFactory)
    {
        return groups
            .Select(group =>
            {
                var entries = group.ToList();
                var studentCount = entries.Count;
                var totalBehaviours = entries.Sum(entry => entry.BehaviourCount);
                return new CohortStatsDto
                {
                    Key = group.Key,
                    Label = labelFactory(group.Key),
                    StudentCount = studentCount,
                    AtRiskCount = entries.Count(entry => entry.Level != "None"),
                    HighRiskCount = entries.Count(entry => entry.Level == "High"),
                    TotalBehaviours = totalBehaviours,
                    TotalProjects = entries.Sum(entry => entry.ProjectCount),
                    AvgBehaviours = studentCount == 0 ? 0 : Math.Round((double)totalBehaviours / studentCount, 1)
                };
            })
            .OrderBy(cohort => cohort.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<StudentProfile>> LoadVisibleWithActivity()
    {
        return await VisibleStudents()
            .AsSplitQuery()
            .Include(student => student.SISClassMemberships)
                .ThenInclude(membership => membership.SISClass)
                .ThenInclude(sisClass => sisClass.Teachers)
                .ThenInclude(assignment => assignment.Teacher)
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .Include(student => student.MentalHealthRecords)
            .Include(student => student.BusAttendanceRecords)
            .ToListAsync();
    }

    private async Task<List<StudentSignalRow>> LoadVisibleSignals(StudentClassificationSettingsDto settings)
    {
        var students = await LoadVisibleWithActivity();
        var now = DateTime.UtcNow;
        var riskSince = now.AddDays(-settings.RiskWindowDays);
        var commendationSince = now.AddDays(-settings.CommendationWindowDays);
        var studentIds = students.Select(student => student.Id).ToList();
        var scoreRows = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.CourseScore && score.StudentProfileId != null
                && studentIds.Contains(score.StudentProfileId.Value)
                && (score.FinalScore ?? score.CurrentScore) != null)
            .Select(score => new
            {
                StudentId = score.StudentProfileId!.Value,
                Value = (decimal)(score.FinalScore ?? score.CurrentScore)!.Value,
                Date = score.UpdatedAt ?? score.CreatedAt
            })
            .ToListAsync();
        var canvasByStudent = scoreRows
            .GroupBy(score => score.StudentId)
            .ToDictionary(
                group => group.Key,
                group => new { Average = Math.Round(group.Average(score => score.Value), 1), Latest = group.Max(score => score.Date) });

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

    private async Task<Dictionary<int, decimal>> CanvasAverages(IReadOnlyCollection<int> studentIds)
    {
        if (studentIds.Count == 0) return [];
        var scores = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.CourseScore && score.StudentProfileId != null
                && studentIds.Contains(score.StudentProfileId.Value)
                && (score.FinalScore ?? score.CurrentScore) != null)
            .Select(score => new
            {
                StudentId = score.StudentProfileId!.Value,
                Value = (decimal)(score.FinalScore ?? score.CurrentScore)!.Value
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
