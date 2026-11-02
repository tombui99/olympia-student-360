using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OlympiaStudent360.Api.Data;

/// <summary>Model-only tooling must not run API startup migrations, seeds or background workers.</summary>
public sealed class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseResilientMySql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=127.0.0.1;Database=olympia_design_time;User=unused;Password=unused")
        .Options);
}
