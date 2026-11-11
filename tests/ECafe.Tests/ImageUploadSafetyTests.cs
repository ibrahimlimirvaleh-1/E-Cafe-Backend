using ECafe.Application.DTOs.File;
using ECafe.Application.Services.FileValidation;
using ECafe.Application.Services.ImageProcessing.Concrete;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ImageUploadSafetyTests
{
    [Theory]
    [InlineData("jpeg", "webp")]
    [InlineData("png", "webp")]
    [InlineData("webp", "webp")]
    [InlineData("avif", "webp")]
    [InlineData("jpeg", "avif")]
    [InlineData("png", "avif")]
    [InlineData("webp", "avif")]
    [InlineData("avif", "avif")]
    public async Task SupportedImagesAreResizedAndEncodedWithCorrectMetadata(string input, string output)
    {
        using var manager = CreateManager(("OutputFormat", output), ("MaxWidth", "8"), ("MaxHeight", "8"));
        var file = CreateFile(CreateImage(input), input);

        var result = await manager.OptimizeForUploadAsync(file, Policy(), FileTypeCode.MenuItemImage);

        Assert.True(result.IsOptimized);
        Assert.Equal($"photo.{output}", result.FileName);
        Assert.Equal($"image/{output}", result.ContentType);
        Assert.Equal(result.Bytes!.LongLength, result.Size);
        await FileUploadValidation.ValidateSignatureAsync(CreateFile(result.Bytes, output), result.ContentType);
        using var decoded = new MagickImage(result.Bytes);
        Assert.Equal(8u, decoded.Width);
        Assert.Equal(6u, decoded.Height);
        Assert.Null(decoded.GetAttribute("comment"));
        Assert.Empty(decoded.ProfileNames);
    }

    [Theory]
    [InlineData(FileTypeCode.RestaurantImage)]
    [InlineData(FileTypeCode.MenuItemImage)]
    [InlineData(FileTypeCode.UserProfileImage)]
    [InlineData(FileTypeCode.TemporaryUpload)]
    public async Task ExistingImagePurposesStillOptimize(FileTypeCode purpose)
    {
        using var manager = CreateManager();
        var result = await manager.OptimizeForUploadAsync(CreateFile(CreateImage("png"), "png"), Policy(), purpose);
        Assert.True(result.IsOptimized);
    }

    [Fact]
    public async Task MimeCaseAndOptionalParametersRemainSupported()
    {
        using var manager = CreateManager();
        var file = CreateFile(CreateImage("png"), "png");
        file.ContentType = "IMAGE/PNG; charset=binary";
        Assert.True((await manager.OptimizeForUploadAsync(file, Policy(), FileTypeCode.MenuItemImage)).IsOptimized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData(";;")]
    public void EmptyMimeDoesNotThrowAnUnexpectedParserException(string? value)
        => Assert.Equal(string.Empty, FileUploadValidation.NormalizeContentType(value));

    [Fact]
    public async Task FileExtensionStillHasToMatchDeclaredMime()
    {
        using var manager = CreateManager();
        var file = MockFile(CreateImage("png"), "png");
        file.SetupGet(x => x.FileName).Returns("photo.jpg");
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage));
        Assert.Equal(ErrorCode.FileExtensionMismatch, error.Code);
        file.Verify(x => x.OpenReadStream(), Times.Never);
    }

    [Fact]
    public async Task CancellationDuringSignatureReadReleasesProcessingSlot()
    {
        using var manager = CreateManager(("MaxConcurrentOperations", "1"));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bytes = CreateImage("png");
        var file = MockFile(bytes, "png");
        file.Setup(x => x.OpenReadStream()).Returns(() => new GatedStream(bytes, entered, release));
        var processing = manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
        Assert.True((await manager.OptimizeForUploadAsync(CreateFile(bytes, "png"), Policy(), FileTypeCode.MenuItemImage)).IsOptimized);
    }

    [Theory]
    [InlineData("tiff", "jpeg")]
    [InlineData("jpeg", "png")]
    [InlineData("png", "webp")]
    public async Task DisguisedFormatsAreRejectedBeforeDecoder(string actual, string declared)
    {
        using var manager = CreateManager();
        var file = MockFile(CreateImage(actual), declared);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.RestaurantImage));
        Assert.Equal(ErrorCode.FileContentTypeMismatch, error.Code);
        file.Verify(x => x.OpenReadStream(), Times.Once);
    }

    [Fact]
    public async Task CorruptImageIsAControlledValidationFailureAndDoesNotLeakSlot()
    {
        using var manager = CreateManager(("MaxConcurrentOperations", "1"));
        byte[] corrupt = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(CreateFile(corrupt, "png"), Policy(), FileTypeCode.RestaurantImage));
        Assert.Equal(ErrorCode.FileContentTypeMismatch, error.Code);
        Assert.True((await manager.OptimizeForUploadAsync(CreateFile(CreateImage("png"), "png"), Policy(), FileTypeCode.RestaurantImage)).IsOptimized);
    }

    [Theory]
    [InlineData("MaxInputDimension", "8", 1)]
    [InlineData("MaxInputPixels", "100", 1)]
    [InlineData("MaxInputFrames", "1", 2)]
    [InlineData("MaxInputPixels", "383", 2)]
    public async Task DimensionsFramesAndAggregatePixelsAreRejectedBeforeRasterDecode(string setting, string value, int frames)
    {
        using var manager = CreateManager((setting, value));
        var file = MockFile(CreateImage("webp", frames), "webp");
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage));
        Assert.Equal(ErrorCode.ImageLimitsExceeded, error.Code);
        file.Verify(x => x.OpenReadStream(), Times.Exactly(2));
    }

    [Theory]
    [InlineData("webp", 2)]
    [InlineData("avif", 1)]
    public async Task AnimatedWebpKeepsFramesAndAvifKeepsExistingFirstFrameBehavior(string output, int expectedFrames)
    {
        using var manager = CreateManager(("OutputFormat", output));
        var result = await manager.OptimizeForUploadAsync(CreateFile(CreateImage("webp", 2), "webp"), Policy(), FileTypeCode.MenuItemImage);
        using var images = new MagickImageCollection(result.Bytes!);
        Assert.Equal(expectedFrames, images.Count);
    }

    [Fact]
    public async Task OversizedPayloadIsRejectedWithoutOpeningStream()
    {
        using var manager = CreateManager();
        var file = MockFile([], "png");
        file.SetupGet(x => x.Length).Returns(11 * 1024L * 1024L);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage));
        Assert.Equal(ErrorCode.FileTooLarge, error.Code);
        file.Verify(x => x.OpenReadStream(), Times.Never);
    }

    [Theory]
    [InlineData("Enabled", "false", FileTypeCode.MenuItemImage)]
    [InlineData("OutputFormat", "jpeg", FileTypeCode.MenuItemImage)]
    [InlineData("Enabled", "true", FileTypeCode.ContractDocument)]
    public async Task ExistingPassthroughCasesAreNotDecoded(string key, string value, FileTypeCode purpose)
    {
        using var manager = CreateManager((key, value));
        var file = MockFile(CreateImage("png"), "png");
        Assert.False((await manager.OptimizeForUploadAsync(file.Object, Policy(), purpose)).IsOptimized);
        file.Verify(x => x.OpenReadStream(), Times.Never);
    }

    [Fact]
    public async Task OutputPolicyStillControlsWhetherConversionIsAllowed()
    {
        using var manager = CreateManager();
        var policy = new FileUploadPolicy { AllowedExtensions = ".png", AllowedMimeTypes = "image/png", MaxSizeMb = 10 };
        Assert.False((await manager.OptimizeForUploadAsync(CreateFile(CreateImage("png"), "png"), policy, FileTypeCode.MenuItemImage)).IsOptimized);
    }

    [Fact]
    public async Task ConcurrentQueueIsBoundedAndCancelledWaitersDoNotLeakSlots()
    {
        using var manager = CreateManager(("MaxConcurrentOperations", "1"), ("QueueTimeoutSeconds", "1"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bytes = CreateImage("png");
        var file = MockFile(bytes, "png");
        file.Setup(x => x.OpenReadStream()).Returns(() => new GatedStream(bytes, entered, release));
        var first = manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var waiting = manager.OptimizeForUploadAsync(CreateFile(bytes, "png"), Policy(), FileTypeCode.MenuItemImage, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => manager.OptimizeForUploadAsync(CreateFile(bytes, "png"), Policy(), FileTypeCode.MenuItemImage));
            Assert.Equal(ErrorCode.ImageProcessingBusy, error.Code);
        }
        finally
        {
            release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(15));
        }
        Assert.True((await manager.OptimizeForUploadAsync(CreateFile(bytes, "png"), Policy(), FileTypeCode.MenuItemImage)).IsOptimized);
    }

    [Fact]
    public async Task CancellationBeforeProcessingDoesNotOpenTheFile()
    {
        using var manager = CreateManager();
        var file = MockFile([], "png");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.OptimizeForUploadAsync(file.Object, Policy(), FileTypeCode.MenuItemImage, new CancellationToken(true)));
        file.Verify(x => x.OpenReadStream(), Times.Never);
    }

    [Fact]
    public async Task SignatureValidationCompletesPartialStreamReads()
    {
        var bytes = CreateImage("png");
        var file = MockFile(bytes, "png");
        file.Setup(x => x.OpenReadStream()).Returns(() => new PartialReadStream(bytes));
        await FileUploadValidation.ValidateSignatureAsync(file.Object, "image/png");
    }

    [Theory]
    [InlineData("application/pdf", "255044462D312E37")]
    [InlineData("application/msword", "D0CF11E0A1B11AE1")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "504B0304")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "504B0506")]
    public async Task ExistingDocumentSignaturesRemainAccepted(string mime, string hex)
        => await FileUploadValidation.ValidateSignatureAsync(new FormFile(new MemoryStream(Convert.FromHexString(hex)), 0, hex.Length / 2, "file", "document") { Headers = new HeaderDictionary(), ContentType = mime }, mime);

    [Theory]
    [InlineData("00000018667479706D696631000000006D69663161766966", true)]
    [InlineData("00000018667479706865696300000000686569636D696631", false)]
    [InlineData("00000010667479706D69663161766966", false)]
    [InlineData("00000008667479706176696600000000", false)]
    public async Task AvifBrandParsingDoesNotAcceptHeicOrMinorVersionAsBrand(string hex, bool valid)
    {
        var bytes = Convert.FromHexString(hex);
        var task = FileUploadValidation.ValidateSignatureAsync(CreateFile(bytes, "avif"), "image/avif");
        if (valid) await task;
        else Assert.Equal(ErrorCode.FileContentTypeMismatch, (await Assert.ThrowsAsync<BusinessRuleException>(() => task)).Code);
    }

    [Fact]
    public void SafetyErrorsHaveAllSupportedTranslations()
    {
        var bootstrap = new TranslationBootstrap();
        foreach (var code in new[] { ErrorCode.ImageLimitsExceeded, ErrorCode.ImageProcessingBusy })
        {
            var entry = bootstrap.Snapshot.Entries[$"api.error.{code}"];
            TranslationPolicy.ValidateText(entry);
            Assert.False(string.IsNullOrWhiteSpace(entry.Az));
            Assert.False(string.IsNullOrWhiteSpace(entry.En));
            Assert.False(string.IsNullOrWhiteSpace(entry.Ru));
        }
    }

    /// <summary>Kiçik test şəkilləri üçün həqiqi kodekləri işə salır; iki fərqli kadr animasiyanın birləşdirilməsinin qarşısını alır.</summary>
    private static byte[] CreateImage(string format, int frames = 1)
    {
        using var images = new MagickImageCollection();
        for (var i = 0; i < frames; i++)
            images.Add(new MagickImage(i % 2 == 0 ? MagickColors.Red : MagickColors.Blue, 16, 12) { AnimationDelay = 10, Comment = "private metadata" });
        return images.ToByteArray(Enum.Parse<MagickFormat>(format, true));
    }

    /// <summary>Xarici servisə çıxmadan emal ayarlarının test üçün ayrıca nüsxəsini qurur.</summary>
    private static ImageProcessingManager CreateManager(params (string Key, string Value)[] settings)
        => new(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(x => "ImageProcessing:" + x.Key, x => (string?)x.Value)).Build());

    /// <summary>Mövcud raster formatlarını və on meqabayt həddini test siyasətində saxlayır.</summary>
    private static FileUploadPolicy Policy() => new() { AllowedExtensions = ".jpg,.jpeg,.png,.webp,.avif", AllowedMimeTypes = "image/jpeg,image/png,image/webp,image/avif", MaxSizeMb = 10 };

    /// <summary>İstək faylını kiçik yaddaş stream-i ilə modelləşdirir.</summary>
    private static FormFile CreateFile(byte[] bytes, string format)
        => new(new MemoryStream(bytes), 0, bytes.Length, "file", $"photo.{format}") { Headers = new HeaderDictionary(), ContentType = "image/" + format };

    /// <summary>Stream-in açılma sayını ölçərək rəddin dekoderdən əvvəl baş verdiyini yoxlamağa imkan verir.</summary>
    private static Mock<IFormFile> MockFile(byte[] bytes, string format)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(x => x.FileName).Returns($"photo.{format}");
        file.SetupGet(x => x.ContentType).Returns("image/" + format);
        file.SetupGet(x => x.Length).Returns(bytes.LongLength);
        file.Setup(x => x.OpenReadStream()).Returns(() => new MemoryStream(bytes));
        return file;
    }

    private sealed class GatedStream(byte[] bytes, TaskCompletionSource entered, TaskCompletionSource release) : MemoryStream(bytes)
    {
        /// <summary>Yalnız testdə ilk oxunuşu saxlayaraq digər sorğuların semafor növbəsini deterministik yoxlayır.</summary>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class PartialReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <summary>Bir oxunuşda bir bayt verən stream başlıq oxunuşunun qısa cavablara davamlılığını yoxlayır.</summary>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
