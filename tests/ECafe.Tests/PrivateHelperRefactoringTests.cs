using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using ECafe.Application.Common.Configuration;
using ECafe.Application.Common.Security;
using ECafe.Application.Common.Validation;
using ECafe.Application.DTOs.Item;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Features.Commands.Restaurant;
using ECafe.Application.Repositories.User;
using ECafe.Application.Services.Auth;
using ECafe.Application.Services.FileValidation;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Shared.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class PrivateHelperRefactoringTests
{
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("synthetic + / = token")]
    [InlineData("sintetik-şifrə-ə")]
    public void SharedTokenHashPreservesStoredUtf8UppercaseSha256Format(string token)
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        Assert.Equal(expected, AuthenticationTokenHelper.HashToken(token));
        Assert.Matches("^[A-F0-9]{64}$", AuthenticationTokenHelper.HashToken(token));
    }

    [Fact]
    public void GeneratedPasswordTokensKeep64BytesAndUrlSafeFormat()
    {
        var tokens = Enumerable.Range(0, 20).Select(_ => AuthenticationTokenHelper.GenerateUrlSafeToken()).ToArray();
        Assert.Equal(tokens.Length, tokens.Distinct().Count());
        foreach (var token in tokens)
        {
            Assert.Matches("^[A-Za-z0-9_-]{86}$", token);
            Assert.Equal(64, Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "==").Length);
        }
    }

    [Fact]
    public void InitialPasswordHashesRemainRandomBcryptAndRejectKnownPasswords()
    {
        var first = AuthenticationTokenHelper.CreateUnusablePasswordHash();
        var second = AuthenticationTokenHelper.CreateUnusablePasswordHash();
        Assert.NotEqual(first, second);
        Assert.StartsWith("$2", first);
        Assert.False(BCrypt.Net.BCrypt.Verify("password", first));
        Assert.False(BCrypt.Net.BCrypt.Verify(string.Empty, first));
    }

    [Theory]
    [InlineData(null, 7)]
    [InlineData("", 7)]
    [InlineData("0", 7)]
    [InlineData("-1", 7)]
    [InlineData("invalid", 7)]
    [InlineData("2147483648", 7)]
    [InlineData("1", 1)]
    [InlineData(" 15 ", 15)]
    [InlineData("2147483647", int.MaxValue)]
    public void PositiveSettingPreservesEachCallersFallback(string? value, int expected)
    {
        var configuration = Configuration("Test:Value", value);
        Assert.Equal(expected, ConfigurationValueReader.GetPositiveInt(configuration, "Test:Value", 7));
        Assert.Equal(300, ConfigurationValueReader.GetPositiveInt(configuration, "Other:Value", 300));
    }

    [Theory]
    [InlineData(null, "http://localhost:5173")]
    [InlineData(" ", "http://localhost:5173")]
    [InlineData("https://app.example.test///", "https://app.example.test")]
    public void PasswordLinksKeepFallbackAndEscapedFragmentForBothRoutes(string? value, string expectedBase)
    {
        var configuration = Configuration("Frontend:BaseUrl", value);
        foreach (var path in new[] { "/reset-password", "/set-password" })
        {
            var url = PasswordLinkBuilder.Build(configuration, path, "synthetic +/&=#?");
            Assert.Equal(expectedBase + path + "#token=synthetic%20%2B%2F%26%3D%23%3F", url);
            Assert.Empty(new Uri(url).Query);
        }
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("UTC", true)]
    [InlineData(" Asia/Baku ", true)]
    [InlineData("Azerbaijan Standard Time", true)]
    [InlineData("not-a-time-zone", false)]
    public void RestaurantTimeZoneHelperPreservesSupportedIdentifiers(string? value, bool expected)
        => Assert.Equal(expected, RestaurantValidation.IsKnownTimeZone(value));

    [Theory]
    [InlineData(null, true)]
    [InlineData("UTC", true)]
    [InlineData("invalid-time-zone", false)]
    public void BothRestaurantValidatorsKeepOptionalTimeZoneRules(string? value, bool expected)
    {
        var create = new RegisterRestaurantCommandValidator().Validate(new RegisterRestaurantCommand { TimeZone = value });
        var update = new UpdateRestaurantCommandValidator().Validate(new UpdateRestaurantCommand { TimeZone = value });
        Assert.Equal(expected, !create.Errors.Any(error => error.PropertyName == "TimeZone"));
        Assert.Equal(expected, !update.Errors.Any(error => error.PropertyName == "TimeZone"));
    }

    [Fact]
    public void RestaurantWorkingDaysPreserveNullEmptyAndDuplicateBehavior()
    {
        Assert.True(RestaurantValidation.HaveUniqueWorkingDays(null));
        Assert.True(RestaurantValidation.HaveUniqueWorkingDays([]));
        List<RestaurantWorkingHourDto> hours = [new() { DayOfWeek = DayOfWeek.Monday }, new() { DayOfWeek = DayOfWeek.Tuesday }];
        Assert.True(RestaurantValidation.HaveUniqueWorkingDays(hours));
        hours[1].DayOfWeek = DayOfWeek.Monday;
        Assert.False(RestaurantValidation.HaveUniqueWorkingDays(hours));
        var create = new RegisterRestaurantCommandValidator().Validate(new RegisterRestaurantCommand { WorkingHours = hours });
        var update = new UpdateRestaurantCommandValidator().Validate(new UpdateRestaurantCommand { WorkingHours = hours });
        Assert.Contains(create.Errors, error => error.PropertyName == "WorkingHours");
        Assert.Contains(update.Errors, error => error.PropertyName == "WorkingHours");
    }

    [Theory]
    [InlineData(5001, true)]
    [InlineData(5002, true)]
    [InlineData(5003, true)]
    [InlineData(1, false)]
    [InlineData(1001, false)]
    [InlineData(5000, false)]
    [InlineData(5004, false)]
    [InlineData(-1, false)]
    public void BothItemValidatorsKeepStatusTypeBoundary(int statusId, bool expected)
    {
        Assert.Equal(expected, ItemStatusValidation.IsValid(statusId));
        var create = new CreateItemRequestValidator().Validate(new CreateItemRequest { StatusId = statusId });
        var update = new UpdateItemRequestValidator().Validate(new UpdateItemRequest { StatusId = statusId });
        Assert.Equal(expected, !create.Errors.Any(error => error.PropertyName == "StatusId"));
        Assert.Equal(expected, !update.Errors.Any(error => error.PropertyName == "StatusId"));
    }

    [Fact]
    public void UploadPolicyCopiesEveryLimitWithoutSharingMutableState()
    {
        var fileType = new FileType { AllowedExtensions = ".pdf,.png", AllowedMimeTypes = "application/pdf,image/png", MaxSizeMb = 12 };
        var first = FileUploadPolicyFactory.FromFileType(fileType);
        var second = FileUploadPolicyFactory.FromFileType(fileType);
        Assert.Equal(fileType.AllowedExtensions, first.AllowedExtensions);
        Assert.Equal(fileType.AllowedMimeTypes, first.AllowedMimeTypes);
        Assert.Equal(fileType.MaxSizeMb, first.MaxSizeMb);
        Assert.NotSame(first, second);
        first.MaxSizeMb = 1;
        Assert.Equal(12, second.MaxSizeMb);
        Assert.Equal(12, fileType.MaxSizeMb);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SharedRegistrationCheckKeepsNormalizationAndEmailErrorPriority(bool emailExists, bool phoneExists)
    {
        var users = new Mock<IUserRepository>();
        List<User> existing = [];
        if (emailExists) existing.Add(new User { Email = "test@example.test", Phone = "other" });
        if (phoneExists) existing.Add(new User { Email = "other", Phone = "+994501234567" });
        users.Setup(repository => repository.CheckExistAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) => existing.Any(predicate.Compile()));
        var error = await Record.ExceptionAsync(() => UserRegistrationValidation.EnsureUserDoesNotExistAsync(
            users.Object, " TEST@EXAMPLE.TEST ", "050 123 45 67"));
        if (emailExists || phoneExists)
            Assert.Equal(emailExists ? ErrorCode.UserEmailAlreadyExists : ErrorCode.UserPhoneAlreadyExists,
                Assert.IsType<BusinessRuleException>(error).Code);
        else
            Assert.Null(error);
        Assert.Equal(emailExists ? 1 : 2, users.Invocations.Count);
    }

    [Fact]
    public async Task CompensationSendsExactFileIdWithoutRequestCancellation()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(It.IsAny<DeleteFileCommand>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        await UnattachedFileCompensation.TryDeleteAsync(sender.Object, NullLogger.Instance, 42, "payment proof");
        sender.Verify(value => value.Send(It.Is<DeleteFileCommand>(command => command.FileId == 42), CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("payment proof")]
    [InlineData("refund transfer proof")]
    public async Task CompensationFailureDoesNotThrowOrLogPrivateExceptionText(string uploadKind)
    {
        const string secret = "SYNTHETIC-PRIVATE-EXCEPTION";
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(It.IsAny<DeleteFileCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException(secret));
        var logger = new Mock<ILogger>();
        await UnattachedFileCompensation.TryDeleteAsync(sender.Object, logger.Object, 42, uploadKind);
        var entry = Assert.Single(logger.Invocations, invocation => invocation.Method.Name == "Log");
        Assert.Equal(LogLevel.Warning, entry.Arguments[0]);
        var safe = Assert.IsType<SafeDiagnosticException>(entry.Arguments[3]);
        Assert.DoesNotContain(secret, safe.ToString());
        Assert.Equal($"Could not compensate {uploadKind} upload for file 42. The unattached-file cleanup worker will retry it.", entry.Arguments[2].ToString());
    }

    /// <summary>Yalnız testə aid dəyəri yaddaş konfiqurasiyasına verir; real appsettings və server sirrlərini oxumur.</summary>
    private static IConfiguration Configuration(string key, string? value)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
}
