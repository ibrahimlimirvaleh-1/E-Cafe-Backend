using ECafe.Api.Security;
using ECafe.Application.Common.Errors;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Features.Commands.File;
using ECafe.Application.Services.FileAccess.Concrete;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECafe.Tests;

public sealed class RetiredPaymentTransportTests
{
    [Fact]
    public void RetiredResourceFilterReturnsGoneWithoutReadingRequestBody()
    {
        using var services = new ServiceCollection().AddSingleton<IErrorMessageProvider, ErrorMessageProvider>().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Body = new UnreadableBody();
        var context = new ResourceExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()),
            new List<IFilterMetadata>(), new List<IValueProviderFactory>());
        new RetiredManualPaymentAttribute().OnResourceExecuting(context);
        Assert.Equal(StatusCodes.Status410Gone, Assert.IsType<ObjectResult>(context.Result).StatusCode);
    }

    [Fact]
    public void PaymentReceiptDownloadIsDeniedBeforeAnyUserOrFileOwnershipBranch()
    {
        var policy = new FileAccessPolicy(new HttpContextAccessor());
        var error = Assert.Throws<ForbiddenException>(() => policy.EnsureCurrentUserCanAccess(new ECafe.Domain.Entities.File
        { FileTypeId = (int)FileTypeCode.PaymentReceipt }));
        Assert.Equal(ErrorCode.ManualPaymentRetired, error.Code);
    }

    [Fact]
    public async Task PaymentReceiptUploadIsDeniedBeforeFileStorageOrOptimization()
    {
        var handler = new FileUploadCommandHandler(null!, null!, null!, null!, null!, null!, null!);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new FileUploadCommand
        { FileTypeId = (int)FileTypeCode.PaymentReceipt }, default));
        Assert.Equal(ErrorCode.ManualPaymentRetired, error.Code);
    }

    private sealed class UnreadableBody : MemoryStream
    {
        /// <summary>Filterin həssas request body-ni oxumağa cəhd etmədiyini yoxlayır.</summary>
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Body must not be read.");
    }
}
