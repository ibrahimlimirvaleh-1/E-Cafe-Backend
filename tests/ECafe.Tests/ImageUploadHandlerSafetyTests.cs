using System.Security.Claims;
using AutoMapper;
using ECafe.Application;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.FileType;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.ImageProcessing.Abstract;
using ECafe.Application.Services.ImageProcessing.Concrete;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ImageUploadHandlerSafetyTests
{
    [Theory]
    [InlineData(FileTypeCode.RestaurantImage, true)]
    [InlineData(FileTypeCode.MenuItemImage, true)]
    [InlineData(FileTypeCode.UserProfileImage, false)]
    [InlineData(FileTypeCode.TemporaryUpload, false)]
    public async Task UploadHandlerKeepsStorageTypeOwnershipAndPublicPrivateUrls(FileTypeCode purpose, bool isPublic)
    {
        using var manager = new ImageProcessingManager(Configuration());
        var type = FileType(purpose, isPublic);
        var types = new Mock<IFileTypeRepository>();
        types.Setup(x => x.GetByIdAsync((int)purpose)).ReturnsAsync(type);
        var minio = new Mock<IMinioService>(MockBehavior.Strict);
        UploadGeneratedFileDto? uploaded = null;
        minio.Setup(x => x.UploadFileAsync(It.IsAny<UploadGeneratedFileDto>(), It.IsAny<FileUploadPolicy>()))
            .Callback<UploadGeneratedFileDto, FileUploadPolicy>((dto, _) => uploaded = dto).ReturnsAsync("safe-token");
        if (isPublic) minio.Setup(x => x.GenerateFileUrl("safe-token")).ReturnsAsync("https://storage.test/safe-token");
        var files = new Mock<IFileRepository>();
        files.Setup(x => x.Add(It.IsAny<ECafe.Domain.Entities.File>())).ReturnsAsync((ECafe.Domain.Entities.File file) => file);
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<ECafe.Domain.Entities.File>(It.IsAny<object>()))
            .Returns((object data) => MapFile((FileMapData)data));
        var urls = new Mock<IFileAccessUrlService>();
        urls.Setup(x => x.BuildUrlsAsync(It.IsAny<ECafe.Domain.Entities.File>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("/api/v1/file/42/view", "/api/v1/file/42/download"));
        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("userId", "7")], "test")) } };
        var handler = new FileUploadCommandHandler(minio.Object, files.Object, types.Object, urls.Object, manager, context, mapper.Object);

        var result = await handler.Handle(new FileUploadCommand { File = ImageFile(), FileTypeId = (int)purpose }, CancellationToken.None);

        Assert.NotNull(uploaded);
        Assert.Equal((int)purpose, uploaded.FileTypeId);
        Assert.Equal("image/webp", uploaded.ContentType);
        Assert.Equal("photo.webp", uploaded.FileName);
        Assert.NotEmpty(uploaded.Bytes);
        files.Verify(x => x.Add(It.Is<ECafe.Domain.Entities.File>(file => file.CreatedBy == "7" && file.FileTypeId == (int)purpose && file.Extension == ".webp" && file.Size == uploaded.Bytes.LongLength)), Times.Once);
        files.Verify(x => x.SaveChangesAsync(), Times.Once);
        minio.Verify(x => x.GenerateFileUrl(It.IsAny<string>()), isPublic ? Times.Once : Times.Never);
        Assert.Equal(isPublic, result.IsPublic);
        Assert.Equal("/api/v1/file/42/view", result.Url);
        Assert.Equal("/api/v1/file/42/download", result.DownloadUrl);
    }

    [Theory]
    [InlineData(false, ErrorCode.FileContentTypeMismatch)]
    [InlineData(true, ErrorCode.ImageLimitsExceeded)]
    public async Task RejectedImagesNeverReachStorageOrDatabase(bool oversizedDimensions, ErrorCode expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ImageProcessing:MaxInputDimension"] = "8" }).Build();
        using var manager = new ImageProcessingManager(configuration);
        var type = FileType(FileTypeCode.MenuItemImage, true);
        var types = new Mock<IFileTypeRepository>();
        types.Setup(x => x.GetByIdAsync(type.Id)).ReturnsAsync(type);
        var minio = new Mock<IMinioService>(MockBehavior.Strict);
        var files = new Mock<IFileRepository>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var urls = new Mock<IFileAccessUrlService>(MockBehavior.Strict);
        var file = oversizedDimensions ? ImageFile() : new FormFile(new MemoryStream([0x49, 0x49, 0x2A, 0x00]), 0, 4, "file", "photo.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var handler = new FileUploadCommandHandler(minio.Object, files.Object, types.Object, urls.Object, manager, new HttpContextAccessor(), mapper.Object);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new FileUploadCommand { File = file, FileTypeId = type.Id }, CancellationToken.None));

        Assert.Equal(expected, error.Code);
        minio.VerifyNoOtherCalls();
        files.VerifyNoOtherCalls();
        mapper.VerifyNoOtherCalls();
        urls.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProcessorIsSingletonSoConcurrentRequestScopesShareTheLimit()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration());
        services.AddApplication();
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, x => x.ServiceType == typeof(IImageProcessingService)).Lifetime);
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<IImageProcessingService>(), second.ServiceProvider.GetRequiredService<IImageProcessingService>());
    }

    /// <summary>Xarici konfiqurasiyaya və məxfi ayarlara toxunmadan standart emal ayarlarını yaradır.</summary>
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection().Build();

    /// <summary>Yükləmənin ictimai və şəxsi məqsədlərini mövcud format siyasəti ilə qurur.</summary>
    private static ECafe.Domain.Entities.FileType FileType(FileTypeCode purpose, bool isPublic)
        => new() { Id = (int)purpose, Code = purpose.ToString(), IsPublic = isPublic, MaxSizeMb = 10, AllowedExtensions = ".png,.webp,.avif", AllowedMimeTypes = "image/png,image/webp,image/avif" };

    /// <summary>Handler inteqrasiyası üçün kiçik həqiqi PNG yaradır.</summary>
    private static FormFile ImageFile()
    {
        using var image = new MagickImage(MagickColors.Red, 16, 12);
        var bytes = image.ToByteArray(MagickFormat.Png);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "photo.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }

    /// <summary>Mapper sərhədində saxlanan fayl adı, ölçü və məqsədin dəyişmədiyini yoxlamaq üçün DTO-nu entity-yə çevirir.</summary>
    private static ECafe.Domain.Entities.File MapFile(FileMapData data)
        => new() { Id = 42, Token = data.Token, Name = Path.GetFileNameWithoutExtension(data.FileName), Extension = Path.GetExtension(data.FileName), Size = data.Size, Url = data.Url, FileTypeId = data.FileTypeId };
}
