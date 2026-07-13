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
