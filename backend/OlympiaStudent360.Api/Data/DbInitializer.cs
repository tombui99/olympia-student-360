using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;

namespace OlympiaStudent360.Api.Data;

public static class DbInitializer
{
    public static async Task Seed(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        await context.MigrateWithRecoveryAsync(cancellationToken);
        await SeedStudentRecordFieldOptions(context, cancellationToken);

        await RenameRole("Principal", "SchoolAdministration", userManager, roleManager, logger);

        string[] roles = { "Admin", "SchoolAdministration", "Teacher" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await RemoveUnsupportedRole("Leadership", userManager, roleManager, logger);
        await RemoveUnsupportedRole("Nurse", userManager, roleManager, logger);

        var adminPassword = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Password is not configured. Skipping default admin user seeding.");
            return;
        }

        var adminUsername = configuration["SeedAdmin:Username"] ?? "admin";
        var adminUser = await userManager.Users.FirstOrDefaultAsync(
            u => u.UserName == adminUsername, cancellationToken);
        if (adminUser != null)
        {
            return;
        }

        adminUser = new ApplicationUser
        {
            UserName = adminUsername,
            Email = configuration["SeedAdmin:Email"] ?? "admin@olympia.edu.vn",
            FullName = configuration["SeedAdmin:FullName"] ?? "Olympia Administrator",
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

    private static async Task SeedStudentRecordFieldOptions(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var additions = new List<StudentRecordFieldOption>();

        if (!await context.StudentRecordFieldOptions.AnyAsync(item =>
                item.ModuleId == StudentRecordFieldOptionService.BehaviourModule &&
                item.FieldKey == StudentRecordFieldOptionService.BehaviourTypeField,
            cancellationToken))
        {
            var sortOrder = 0;
            additions.AddRange(StudentClassificationService.PositiveBehaviourTypes.Select(value =>
                new StudentRecordFieldOption
                {
                    ModuleId = StudentRecordFieldOptionService.BehaviourModule,
                    FieldKey = StudentRecordFieldOptionService.BehaviourTypeField,
                    Value = value,
                    OptionGroup = "positive",
                    SortOrder = sortOrder++
                }));
            additions.AddRange(StudentClassificationService.ConcerningBehaviourTypes.Select(value =>
                new StudentRecordFieldOption
                {
                    ModuleId = StudentRecordFieldOptionService.BehaviourModule,
                    FieldKey = StudentRecordFieldOptionService.BehaviourTypeField,
                    Value = value,
                    OptionGroup = "concerning",
                    SortOrder = sortOrder++
                }));
        }

        AddProjectOptionsIfMissing(
            await context.StudentRecordFieldOptions.AnyAsync(item =>
                item.ModuleId == StudentRecordFieldOptionService.ProjectModule &&
                item.FieldKey == StudentRecordFieldOptionService.ProjectSubjectField,
            cancellationToken),
            StudentRecordFieldOptionService.ProjectSubjectField,
            StudentDataEntryOptions.ProjectSubjects,
            additions);
        AddProjectOptionsIfMissing(
            await context.StudentRecordFieldOptions.AnyAsync(item =>
                item.ModuleId == StudentRecordFieldOptionService.ProjectModule &&
                item.FieldKey == StudentRecordFieldOptionService.ProjectStatusField,
            cancellationToken),
            StudentRecordFieldOptionService.ProjectStatusField,
            StudentDataEntryOptions.ProjectStatuses,
            additions);

        if (additions.Count == 0) return;
        context.StudentRecordFieldOptions.AddRange(additions);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddProjectOptionsIfMissing(
        bool alreadyConfigured,
        string fieldKey,
        IReadOnlyList<string> values,
        ICollection<StudentRecordFieldOption> additions)
    {
        if (alreadyConfigured) return;
        for (var index = 0; index < values.Count; index++)
        {
            additions.Add(new StudentRecordFieldOption
            {
                ModuleId = StudentRecordFieldOptionService.ProjectModule,
                FieldKey = fieldKey,
                Value = values[index],
                SortOrder = index
            });
        }
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

    private static async Task RenameRole(
        string oldName,
        string newName,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        var oldRole = await roleManager.FindByNameAsync(oldName);
        if (oldRole == null)
        {
            return;
        }

        var newRole = await roleManager.FindByNameAsync(newName);
        if (newRole == null)
        {
            oldRole.Name = newName;
            var renameResult = await roleManager.UpdateAsync(oldRole);
            if (!renameResult.Succeeded)
            {
                var errors = string.Join("; ", renameResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Role '{oldName}' could not be renamed: {errors}");
            }

            logger.LogInformation("Renamed role '{OldRole}' to '{NewRole}'.", oldName, newName);
            return;
        }

        var usersInOldRole = await userManager.GetUsersInRoleAsync(oldName);
        foreach (var user in usersInOldRole)
        {
            if (!await userManager.IsInRoleAsync(user, newName))
            {
                await userManager.AddToRoleAsync(user, newName);
            }
            await userManager.RemoveFromRoleAsync(user, oldName);
        }

        var deleteResult = await roleManager.DeleteAsync(oldRole);
        if (!deleteResult.Succeeded)
        {
            var errors = string.Join("; ", deleteResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Legacy role '{oldName}' could not be removed: {errors}");
        }
    }
}
