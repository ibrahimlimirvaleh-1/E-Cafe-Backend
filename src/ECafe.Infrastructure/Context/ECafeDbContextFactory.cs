using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Context;

public sealed class ECafeDbContextFactory : IDesignTimeDbContextFactory<ECafeDbContext>
{
    public ECafeDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("src/ECafe.Api/appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("src/ECafe.Api/appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var cs = configuration.GetConnectionString("ECafeDb");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'ECafeDb' not found in appsettings or environment.");

        var optionsBuilder = new DbContextOptionsBuilder<ECafeDbContext>();
        optionsBuilder.UseNpgsql(cs);

        return new ECafeDbContext(optionsBuilder.Options);
    }
}
