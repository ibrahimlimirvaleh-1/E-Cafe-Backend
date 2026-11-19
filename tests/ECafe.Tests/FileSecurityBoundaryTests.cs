using System.Security.Claims;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.File;
using ECafe.Application.Features.Queries.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.FileAccess.Concrete;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using StoredFile = ECafe.Domain.Entities.File;

namespace ECafe.Tests;

public sealed class FileSecurityBoundaryTests
{
    [Theory]
    [InlineData(5, false, "5", ".png", FileTypeCode.TemporaryUpload, true)]
    [InlineData(5, false, "42", ".png", FileTypeCode.TemporaryUpload, false)]
    [InlineData(0, false, "0", ".png", FileTypeCode.TemporaryUpload, false)]
    [InlineData(5, false, "5", ".pdf", FileTypeCode.TemporaryUpload, false)]
    [InlineData(5, false, "5", ".png", FileTypeCode.ContractDocument, false)]
    [InlineData(5, true, "42", ".webp", FileTypeCode.TemporaryUpload, true)]
    [InlineData(5, true, "42", ".pdf", FileTypeCode.ContractDocument, false)]
    [InlineData(5, false, "5", ".JPG", FileTypeCode.UserProfileImage, true)]
    public void ImageAttachmentRequiresUploaderOwnershipAndImageSource(int actor, bool admin,
        string uploader, string extension, FileTypeCode type, bool expected)
        => Assert.Equal(expected, FileAttachmentPolicy.CanAttachImage(new StoredFile
        { CreatedBy = uploader, Extension = extension, FileTypeId = (int)type }, actor, admin));

    [Theory]
    [InlineData(RoleCode.Waiter, false)]
    [InlineData(RoleCode.Kitchen, false)]
    [InlineData(RoleCode.Manager, true)]
    [InlineData(RoleCode.Owner, true)]
    public async Task ContractDownloadsUseCurrentTenantPermissionBeforeStorage(RoleCode assignedRole, bool allowed)
    {
        using var services = Services(assignedRole, out var context);
        var policy = new FileAccessPolicy(new HttpContextAccessor { HttpContext = context }, services.GetRequiredService<IAuthorizationService>());
        var file = ContractFile();
        var repository = new Mock<IFileRepository>();
        repository.Setup(value => value.GetWithUsageByIdAsync(10)).ReturnsAsync(file);
        var storage = new Mock<IMinioService>();
        storage.Setup(value => value.GetFileAsync(file.Token)).ReturnsAsync(new GetFileResponse(new byte[] { 1 }, "application/pdf"));
        var handler = new DownloadFileQueryHandler(repository.Object, storage.Object, policy);
        if (allowed)
        {
            var response = await handler.Handle(new DownloadFileQuery { FileId = 10 }, default);
            Assert.Equal("contract.pdf", response.FileName);
        }
        else
            await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new DownloadFileQuery { FileId = 10 }, default));
        storage.Verify(value => value.GetFileAsync(It.IsAny<string>()), allowed ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task AnotherRestaurantsOwnerPermissionCannotAuthorizeThisContract()
    {
        using var services = Services(RoleCode.Waiter, out var context);
        context.Request.Headers["X-Active-Restaurant-Id"] = "2";
        var policy = new FileAccessPolicy(new HttpContextAccessor { HttpContext = context }, services.GetRequiredService<IAuthorizationService>());
        await Assert.ThrowsAsync<ForbiddenException>(() => policy.EnsureCurrentUserCanAccessAsync(ContractFile()));
    }

    [Fact]
    public async Task ContractTenantAndRetiredReceiptChecksRemainFailClosed()
    {
        using var services = Services(RoleCode.Owner, out var context);
        var policy = new FileAccessPolicy(new HttpContextAccessor { HttpContext = context }, services.GetRequiredService<IAuthorizationService>());
        var file = ContractFile();
        file.RestaurantContracts.Single().RestaurantId = 99;
        await Assert.ThrowsAsync<ForbiddenException>(() => policy.EnsureCurrentUserCanAccessAsync(file));
        Assert.Throws<ForbiddenException>(() => policy.EnsureCurrentUserCanAccess(file));
        file.FileTypeId = (int)FileTypeCode.PaymentReceipt;
        var error = await Assert.ThrowsAsync<ForbiddenException>(() => policy.EnsureCurrentUserCanAccessAsync(file));
        Assert.Equal(ErrorCode.ManualPaymentRetired, error.Code);
    }

    /// <summary>Qlobal sahibkar rolundan deyil, cari restoran aidiyyəti və mərkəzi icazədən istifadə edən real authorization zənciri qurur.</summary>
    private static ServiceProvider Services(RoleCode assignedRole, out DefaultHttpContext context)
    {
        context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", "5"),
                new Claim(ClaimTypes.Role, ((int)RoleCode.Owner).ToString()), new Claim("restaurantId", "1") }, "test"))
        };
        var permissions = new Mock<IPermissionCacheService>();
        permissions.Setup(value => value.GetPermissionsAsync(It.IsAny<int>())).ReturnsAsync((int role) =>
            role is (int)RoleCode.Owner or (int)RoleCode.Manager ? new List<int> { (int)PermissionCode.ViewRestaurantContracts } : new List<int>());
        var access = new Mock<IUserRestaurantAccessCache>();
        access.Setup(value => value.GetActiveRoleIdAsync(5, 1)).ReturnsAsync((int)assignedRole);
        access.Setup(value => value.GetActiveRoleIdAsync(5, 2)).ReturnsAsync((int)RoleCode.Owner);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context });
        services.AddSingleton(permissions.Object);
        services.AddSingleton(access.Object);
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services.BuildServiceProvider();
    }

    /// <summary>Heç bir real sənəd və ya storage ünvanı olmayan məxfi test müqaviləsi yaradır.</summary>
    private static StoredFile ContractFile() => new()
    {
        Id = 10, Name = "contract", Extension = ".pdf", Token = "synthetic-contract", FileTypeId = (int)FileTypeCode.ContractDocument,
        RestaurantContracts = new List<RestaurantContract> { new() { RestaurantId = 1 } }
    };
}
