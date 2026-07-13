using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Data;

public static class DbInitializer
{
    public static async Task Seed(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await context.Database.MigrateAsync();

        string[] roles = { "Admin", "Principal", "Teacher" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await RemoveUnsupportedRole("Leadership", userManager, roleManager, logger);
        await SeedStudentDemoData(context);

        var adminPassword = app.Configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Password is not configured. Skipping default admin user seeding.");
            return;
        }

        var adminUsername = app.Configuration["SeedAdmin:Username"] ?? "admin";
        var adminUser = await userManager.Users.FirstOrDefaultAsync(u => u.UserName == adminUsername);
        if (adminUser != null)
        {
            return;
        }

        adminUser = new ApplicationUser
        {
            UserName = adminUsername,
            Email = app.Configuration["SeedAdmin:Email"] ?? "admin@olympia.edu.vn",
            FullName = app.Configuration["SeedAdmin:FullName"] ?? "Olympia Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        logger.LogWarning("Default admin user was not seeded: {Errors}", errors);
    }

    private static async Task SeedStudentDemoData(AppDbContext context)
    {
        if (!await context.StudentProfiles.AnyAsync())
        {
            context.StudentProfiles.AddRange(
                new StudentProfile { StudentCode = "S1001", FullName = "Nguyễn Minh Anh", GradeLevel = "6", Homeroom = "6A1" },
                new StudentProfile { StudentCode = "S1002", FullName = "Trần Gia Bảo", GradeLevel = "6", Homeroom = "6A2" },
                new StudentProfile { StudentCode = "S1003", FullName = "Lê Khánh Chi", GradeLevel = "7", Homeroom = "7A1" },
                new StudentProfile { StudentCode = "S1004", FullName = "Phạm Đức Duy", GradeLevel = "7", Homeroom = "7A1" },
                new StudentProfile { StudentCode = "S1005", FullName = "Hoàng Thu Hà", GradeLevel = "8", Homeroom = "8A2" },
                new StudentProfile { StudentCode = "S1006", FullName = "Vũ Nam Khánh", GradeLevel = "8", Homeroom = "8A1" },
                new StudentProfile { StudentCode = "S1007", FullName = "Đặng Bảo Linh", GradeLevel = "9", Homeroom = "9A1" },
                new StudentProfile { StudentCode = "S1008", FullName = "Bùi Quang Minh", GradeLevel = "9", Homeroom = "9A2" });
            await context.SaveChangesAsync();
        }

        var hasProfileData =
            await context.StudentBehaviours.AnyAsync() ||
            await context.StudentProjects.AnyAsync();
        if (hasProfileData)
        {
            return;
        }

        var students = await context.StudentProfiles
            .OrderBy(student => student.StudentCode)
            .Take(8)
            .ToListAsync();

        var behaviourTypes = new[]
        {
            "Commendation",
            "Excellent participation",
            "Helped a classmate",
            "Late to class",
            "Homework not submitted",
            "Disruptive in class"
        };
        var projectTemplates = new[]
        {
            ("Science Fair Volcano Model", "Science", "Completed"),
            ("Vietnam History Timeline", "History", "In Progress"),
            ("English Storytelling Podcast", "English", "In Progress"),
            ("Recycled Art Sculpture", "Art", "Planned"),
            ("Math Puzzle Exhibition", "Mathematics", "Completed"),
            ("School Garden Journal", "Biology", "In Progress")
        };

        var now = DateTime.UtcNow;
        for (var index = 0; index < students.Count; index++)
        {
            var student = students[index];
            var behaviourCount = 3 + (index % 4);
            for (var offset = 0; offset < behaviourCount; offset++)
            {
                context.StudentBehaviours.Add(new StudentBehaviour
                {
                    StudentProfileId = student.Id,
                    IncidentDate = now.AddDays(-(offset * 23 + index * 5 + 2)),
                    BehaviourType = behaviourTypes[(index + offset) % behaviourTypes.Length],
                    Notes = "Recorded during homeroom review.",
                    RecordedByName = "Homeroom Teacher"
                });
            }

            var projectCount = 1 + (index % 3);
            for (var offset = 0; offset < projectCount; offset++)
            {
                var (name, subject, status) = projectTemplates[(index + offset) % projectTemplates.Length];
                context.StudentProjects.Add(new StudentProject
                {
                    StudentProfileId = student.Id,
                    ProjectName = name,
                    Subject = subject,
                    Status = status,
                    TeacherNotes = status == "Completed" ? "Presented to the class." : "On track with milestones.",
                    CreatedAt = now.AddDays(-(offset * 40 + index * 7 + 10))
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task RemoveUnsupportedRole(
        string role,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        var existingRole = await roleManager.FindByNameAsync(role);
        if (existingRole == null)
        {
            return;
        }

        var usersInRole = await userManager.GetUsersInRoleAsync(role);
        foreach (var user in usersInRole)
        {
            if (!await userManager.IsInRoleAsync(user, "Teacher"))
            {
                await userManager.AddToRoleAsync(user, "Teacher");
            }

            await userManager.RemoveFromRoleAsync(user, role);
        }

        var result = await roleManager.DeleteAsync(existingRole);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            logger.LogWarning("Unsupported role '{Role}' was not removed: {Errors}", role, errors);
        }
    }
}
