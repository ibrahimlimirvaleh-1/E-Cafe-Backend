using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using ECafe.Api.Controllers;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECafe.Tests;

public sealed class DotNet10CompatibilityTests
{
    [Theory]
    [InlineData(typeof(AuthController))]
    [InlineData(typeof(ECafeDbContext))]
    public void ApplicationAssembliesTargetDotNet10(Type applicationType)
    {
        Assert.Equal(".NETCoreApp,Version=v10.0", applicationType.Assembly
            .GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName);
        Assert.Equal(10, Environment.Version.Major);
        Assert.True(Environment.Version >= new Version(10, 0, 15));
    }

    [Fact]
    public void Ef10DoesNotRequireChangesToTheExistingDatabaseSchema()
    {
        using var context = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=not_connected;Username=test;Password=test").Options);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Authentication.JwtBearer", "10.0.15")]
    [InlineData("Microsoft.AspNetCore.SignalR.StackExchangeRedis", "10.0.15")]
    [InlineData("Microsoft.Extensions.Caching.StackExchangeRedis", "10.0.15")]
    [InlineData("Microsoft.EntityFrameworkCore", "10.0.15")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "10.0.3")]
    public void RuntimeUsesTheAlignedPlatformPackages(string package, string version)
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "ECafe.Api.deps.json");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(manifest));
        var dependency = Assert.Single(document.RootElement.GetProperty("libraries").EnumerateObject(),
            library => library.Name.StartsWith(package + "/", StringComparison.Ordinal));

        Assert.Equal(package + "/" + version, dependency.Name);
    }
}
