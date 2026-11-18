using System.Text;
using System.Text.Json;
using AutoMapper;
using ECafe.Api.Controllers;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.Mappings;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECafe.Tests;

public sealed class DependencySecurityRegressionTests
{
    [Fact]
    public void ApiRuntimeGraphPinsThePatchedPkcsVersion()
    {
        var manifest = Path.Combine(AppContext.BaseDirectory,
            typeof(AuthController).Assembly.GetName().Name + ".deps.json");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(manifest));
        var dependency = Assert.Single(document.RootElement.GetProperty("libraries").EnumerateObject(),
            library => library.Name.StartsWith("System.Security.Cryptography.Pkcs/", StringComparison.Ordinal));

        Assert.Equal("System.Security.Cryptography.Pkcs/9.0.23", dependency.Name);
    }

    [Theory]
    [InlineData("Microsoft.Azure.ServiceBus")]
    [InlineData("Microsoft.Azure.Amqp")]
    [InlineData("Microsoft.Azure.Services.AppAuthentication")]
    [InlineData("Microsoft.IdentityModel.Clients.ActiveDirectory")]
    [InlineData("Newtonsoft.Json")]
    [InlineData("System.Net.Http")]
    [InlineData("System.Text.RegularExpressions")]
    public void RetiredServiceBusDependenciesAreNotInApiRuntimeGraph(string package)
    {
        var manifest = Path.Combine(AppContext.BaseDirectory,
            typeof(AuthController).Assembly.GetName().Name + ".deps.json");
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(manifest));
        var packages = document.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(library => library.Name.Split('/')[0]);

        Assert.DoesNotContain(packages, name => string.Equals(name, package, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegistrationMappingKeepsNormalizationPasswordHashAndCustomerRole()
    {
        var mapper = new MapperConfiguration(config => config.AddProfile<AuthProfile>(),
            NullLoggerFactory.Instance).CreateMapper();
        var request = new RegisterRequestDto
        {
            Name = " Test ", Surname = " Customer ", Email = " TEST@example.test ",
            Phone = "050 123 45 67", Password = "test-only-password", FileId = 42
        };

        var user = mapper.Map<User>(request);

        Assert.Equal("Test", user.Name);
        Assert.Equal("Customer", user.Surname);
        Assert.Equal("test@example.test", user.Email);
        Assert.Equal("+994501234567", user.Phone);
        Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, user.Password));
        Assert.NotEqual(request.Password, user.Password);
        Assert.Equal((int)RoleCode.Customer, user.RoleId);
        Assert.True(user.IsActive);
        Assert.Null(user.FileId);
        Assert.Null(user.File);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ConstructorMappingEnforcesMaxDepthWithoutUnboundedRecursion(int maxDepth)
    {
        var mapper = new MapperConfiguration(config =>
            config.CreateMap<RecursiveInput, RecursiveRecord>().MaxDepth(maxDepth),
            NullLoggerFactory.Instance).CreateMapper();
        var input = new RecursiveInput();
        var tail = input;
        for (var index = 0; index < 12; index++)
        {
            tail.Next = new RecursiveInput();
            tail = tail.Next;
        }

        var result = mapper.Map<RecursiveRecord>(input);

        var depth = 0;
        for (var node = result; node is not null; node = node.Next)
            depth++;
        Assert.InRange(depth, 1, maxDepth);
    }

    [Fact]
    public async Task MultipartRegistrationFieldsAndImageBytesRemainReadable()
    {
        byte[] image = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0x00, 0xff];
        using var multipart = new MultipartFormDataContent("ecafe-test-boundary");
        multipart.Add(new StringContent("Test Customer"), "Name");
        multipart.Add(new ByteArrayContent(image), "File", "photo.png");
        await using var body = new MemoryStream(await multipart.ReadAsByteArrayAsync());
        var context = new DefaultHttpContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = body.Length;
        context.Request.Body = body;

        var form = await context.Request.ReadFormAsync();

        Assert.Equal("Test Customer", form["Name"].ToString());
        var file = Assert.Single(form.Files);
        Assert.Equal("photo.png", file.FileName);
        await using var copied = new MemoryStream();
        await file.CopyToAsync(copied);
        Assert.Equal(image, copied.ToArray());
    }

    [Fact]
    public async Task MultipartBodyLimitStillRejectsOversizedFile()
    {
        using var multipart = new MultipartFormDataContent("ecafe-limit-boundary");
        multipart.Add(new ByteArrayContent(new byte[256]), "File", "photo.png");
        await using var body = new MemoryStream(await multipart.ReadAsByteArrayAsync());
        var context = new DefaultHttpContext();
        context.Request.ContentType = multipart.Headers.ContentType!.ToString();
        context.Request.ContentLength = body.Length;
        context.Request.Body = body;
        context.Features.Set<IFormFeature>(new FormFeature(context.Request,
            new FormOptions { MultipartBodyLengthLimit = 64 }));

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Request.ReadFormAsync());
    }

    [Fact]
    public async Task BufferedReadHonorsLimitAndReleasesTemporaryFile()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 128)));
        var buffered = new FileBufferingReadStream(source, 16, 64, Path.GetTempPath());
        string? tempFile;
        try
        {
            var bytes = new byte[32];
            Assert.Equal(32, await buffered.ReadAsync(bytes.AsMemory()));
            tempFile = buffered.TempFileName;
            Assert.NotNull(tempFile);
            Assert.True(System.IO.File.Exists(tempFile));
            await Assert.ThrowsAsync<IOException>(async () =>
            {
                while (await buffered.ReadAsync(bytes.AsMemory()) > 0) { }
            });
        }
        finally
        {
            await buffered.DisposeAsync();
        }
        Assert.False(System.IO.File.Exists(tempFile));
    }

    public sealed class RecursiveInput
    {
        public int Value { get; set; }
        public RecursiveInput? Next { get; set; }
    }

    public sealed record RecursiveRecord(int Value, RecursiveRecord? Next);
}
