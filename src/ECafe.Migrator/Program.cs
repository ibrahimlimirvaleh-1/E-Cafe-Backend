using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("ECafeDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'ECafeDb' is missing.");

var options = new DbContextOptionsBuilder<ECafeDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var dbContext = new ECafeDbContext(options);

Console.WriteLine("Starting ECafe database migration.");
await dbContext.Database.MigrateAsync();
Console.WriteLine("ECafe database migration completed.");
