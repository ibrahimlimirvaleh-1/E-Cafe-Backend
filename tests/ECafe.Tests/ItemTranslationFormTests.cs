using System.Globalization;
using ECafe.Api.Controllers;
using ECafe.Api.Requests.Item;
using ECafe.Application.Features.Commands.Item;
using ECafe.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ItemTranslationFormTests
{
    [Fact]
    public async Task IndexedCreateFormTranslationsReachCommand()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
        using var services = CreateServices(mediator.Object);
        var request = await BindForm<CreateItemFormRequest>(services, new()
        {
            ["Name"] = "Qəhvə", ["Translations[0].LanguageCode"] = "en",
            ["Translations[0].Name"] = "Coffee", ["Translations[0].Description"] = "Fresh coffee"
        });

        await CreateController(services).Create(3, request);

        mediator.Verify(x => x.Send(It.Is<CreateItemCommand>(command => command.RestaurantId == 3 &&
            command.Translations != null && command.Translations.Count == 1 &&
            command.Translations[0].LanguageCode == "en" && command.Translations[0].Name == "Coffee" &&
            command.Translations[0].Description == "Fresh coffee"), CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateFormDistinguishesPreserveFromExplicitClear(bool clear)
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<UpdateItemCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
        using var services = CreateServices(mediator.Object);
        var form = new Dictionary<string, StringValues> { ["Name"] = "Qəhvə" };
        if (clear) form["ClearTranslations"] = "true";
        var request = await BindForm<UpdateItemFormRequest>(services, form);

        await CreateController(services).Update(3, 7, request);

        mediator.Verify(x => x.Send(It.Is<UpdateItemCommand>(command => command.RestaurantId == 3 &&
            command.ItemId == 7 && (clear ? command.Translations != null && command.Translations.Count == 0 :
                command.Translations == null)), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AmbiguousClearAndReplacementIsRejectedBeforeCommand()
    {
        var mediator = new Mock<IMediator>();
        using var services = CreateServices(mediator.Object);
        var request = await BindForm<UpdateItemFormRequest>(services, new()
        {
            ["ClearTranslations"] = "true", ["Translations[0].LanguageCode"] = "en",
            ["Translations[0].Name"] = "Coffee"
        });

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateController(services).Update(3, 7, request));

        mediator.Verify(x => x.Send(It.IsAny<UpdateItemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Real MVC form bağlayıcısı ilə indeksli tərcümələri oxuyur; JSON fərziyyəsi ilə yanlış testdən qaçır.</summary>
    private static async Task<T> BindForm<T>(IServiceProvider services, Dictionary<string, StringValues> fields)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.ContentType = "multipart/form-data; boundary=test";
        http.Request.Form = new FormCollection(fields);
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var metadata = services.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(T));
        var context = DefaultModelBindingContext.CreateBindingContext(action,
            new FormValueProvider(BindingSource.Form, http.Request.Form, CultureInfo.InvariantCulture),
            metadata, new BindingInfo { BindingSource = BindingSource.Form }, string.Empty);
        var binder = services.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext
        {
            Metadata = metadata, BindingInfo = new BindingInfo { BindingSource = BindingSource.Form }
        });
        await binder.BindModelAsync(context);
        return Assert.IsType<T>(context.Result.Model);
    }

    /// <summary>Controller və MVC model bağlayıcısının asılılıqlarını test üçün yaradır.</summary>
    private static ServiceProvider CreateServices(IMediator mediator)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(mediator);
        services.AddControllers();
        return services.BuildServiceProvider();
    }

    /// <summary>Mediatoru real request xidmətlərindən alan controller nümunəsi qurur.</summary>
    private static ItemController CreateController(IServiceProvider services) => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
    };
}
