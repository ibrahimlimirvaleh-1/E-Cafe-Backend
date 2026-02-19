using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Context;

public sealed class ECafeDbContextFactory : IDesignTimeDbContextFactory<ECafeDbContext>
{
    public ECafeDbContext CreateDbContext(string[] args)
    {
        var solutionRoot = ResolveSolutionRoot();
        var apiProjectPath = Path.Combine(solutionRoot, "src", "ECafe.Api");
        var appsettingsPath = Path.Combine(apiProjectPath, "appsettings.json");

        if (!File.Exists(appsettingsPath))
        {
            throw new InvalidOperationException(
                $"The configuration file '{appsettingsPath}' was not found. " +
                "Run EF commands from the solution root or ensure the API project files exist.");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cs = configuration.GetConnectionString("ECafeDb");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'ECafeDb' not found in appsettings or environment.");

        var optionsBuilder = new DbContextOptionsBuilder<ECafeDbContext>();
        optionsBuilder.UseNpgsql(cs);

        return new ECafeDbContext(optionsBuilder.Options);
    }

    private static string ResolveSolutionRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (current is not null)
        {
            if (current.GetFiles("ECafe.sln", SearchOption.TopDirectoryOnly).Any())
                return current.FullName;

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
