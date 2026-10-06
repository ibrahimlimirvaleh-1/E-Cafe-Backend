using ECafe.Api.Controllers;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.MobileApp;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class MobileAppControllerTests
{
    [Fact]
    public async Task CustomerDownloadChecksCurrentAccountBeforeOpeningArtifact()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<GetCustomerMobileAccessQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("An active customer account is required."));
        var artifacts = new Mock<IMobileReleaseArtifactService>();
        var controller = CreateController(mediator.Object, artifacts.Object, out var services);
        using (services)
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => controller.DownloadCustomerRelease(CancellationToken.None));
        }

        artifacts.Verify(x => x.OpenVerifiedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CustomerDownloadStreamsOnlyVerifiedPrivateArtifact()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<GetCustomerMobileAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerMobileAccessResponse(true));
        var content = new MemoryStream([1, 2, 3]);
        var artifacts = new Mock<IMobileReleaseArtifactService>();
        artifacts.Setup(x => x.OpenVerifiedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMobileRelease(content, "0.1.0", 5, 3, new string('a', 64)));
        var controller = CreateController(mediator.Object, artifacts.Object, out var services);
        using (services)
        {
            var response = Assert.IsType<FileStreamResult>(await controller.DownloadCustomerRelease(CancellationToken.None));
            Assert.Equal("application/vnd.android.package-archive", response.ContentType);
            Assert.Equal("ECafe-0.1.0.apk", response.FileDownloadName);
            Assert.True(response.EnableRangeProcessing);
            Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
            Assert.Equal("no-cache", controller.Response.Headers.Pragma);
            Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions);
        }

        await content.DisposeAsync();
        mediator.Verify(x => x.Send(It.IsAny<GetCustomerMobileAccessQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        artifacts.Verify(x => x.OpenVerifiedAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MobileAppController CreateController(
        IMediator mediator, IMobileReleaseArtifactService artifacts, out ServiceProvider services)
    {
        services = new ServiceCollection().AddSingleton(mediator).BuildServiceProvider();
        return new MobileAppController(Mock.Of<IMobilePushInstallationService>(), artifacts)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
    }
}
