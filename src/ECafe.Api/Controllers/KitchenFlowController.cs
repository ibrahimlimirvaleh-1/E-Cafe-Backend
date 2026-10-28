using ECafe.Application.DTOs.Order;
using ECafe.Application.Features.Commands.Order;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize]
[HasPermission(PermissionCode.ManageKitchenOrders)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KitchenFlowController : BaseController
{
    /// <summary>Mətbəx öz restoranındakı ləğv sorğusunun son nəticəsini, sifariş növbədən çıxsa da yoxlaya bilər.</summary>
    [HttpGet(ApiRoutes.KitchenFlow.CancellationRequest)]
    [ProducesResponseType(typeof(KitchenOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCancellationRequest([FromRoute] int restaurantId, [FromRoute] int orderId, CancellationToken token)
        => Ok(await Mediator.Send(new KitchenCancellationQuery(restaurantId, orderId), token));

    /// <summary>Mətbəx yalnız ilkin sərfiyyatdan hazırlanmış, öz restoranına aid ərzaq seçimini oxuyur.</summary>
    [HttpGet(ApiRoutes.KitchenFlow.StockReturnPreview)]
    [ProducesResponseType(typeof(KitchenStockReturnPreviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockReturnPreview([FromRoute] int restaurantId, [FromRoute] int orderId, CancellationToken token)
        => Ok(await Mediator.Send(new KitchenStockReturnPreviewQuery(restaurantId, orderId), token));

    /// <summary>Mətbəx səbəb ilə qərar verir; tam və ya ərzaq üzrə geri qaytarma bir dəfə icra olunur, ödəniş/refund dəyişmir.</summary>
    [HttpPost(ApiRoutes.KitchenFlow.DecideCancellation)]
    [ProducesResponseType(typeof(KitchenOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DecideCancellation([FromRoute] int restaurantId, [FromRoute] int orderId,
        [FromBody] DecideOrderCancellationRequest request, CancellationToken token)
        => Ok(await Mediator.Send(new DecideCancellationCommand(restaurantId, orderId, request), token));

    /// <summary>Göndərilmiş sifarişi mətbəx tərəfindən qəbul edir və qəbul edən əməkdaşı saxlayır.</summary>
    /// <remarks>Body yoxdur. Yalnız həmin restoranın aktiv Kitchen əməkdaşı üçün. Eyni addımın ilk əməkdaş tərəfindən təkrarı 200, başqa əməkdaşın eyni addımı və yanlış ardıcıllıq 409 qaytarır. Göndərilməmiş sifariş qəbul olunmur. Bir neçə masanın sifarişi ayrıca qəbul edilə bilər.</remarks>
    [HttpPost(ApiRoutes.KitchenFlow.Accept)]
    [ProducesResponseType(typeof(KitchenOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept([FromRoute] int restaurantId, [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new AcceptKitchenOrderCommand(restaurantId, orderId), cancellationToken));

    /// <summary>Qəbul edilmiş sifarişin hazırlanmasına başlayır və vaxtı/əməkdaşı qeyd edir.</summary>
    /// <remarks>Body yoxdur. Aktiv resept və kifayət edən inqrediyent tələb olunur. Resept miqdarı sifariş sayına vurulur, anbar vahidinə çevrilib stokdan bir dəfə silinir. Çatışmazlıq 409 KitchenOrderStockInsufficient, səhv resept 409 KitchenOrderRecipeInvalid qaytarır; status Accepted qalır və heç bir inqrediyent silinmir. Status, stok hərəkətləri və audit eyni transaction-dadır. Təkrar sorğu ikinci silinmə yaratmır. Başqa aktiv Kitchen əməkdaşı işi davam etdirə bilər; ödəniş dəyişmir.</remarks>
    [HttpPost(ApiRoutes.KitchenFlow.StartPreparing)]
    [ProducesResponseType(typeof(KitchenOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartPreparing([FromRoute] int restaurantId, [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new StartKitchenOrderCommand(restaurantId, orderId), cancellationToken));

    /// <summary>Hazırlanan sifarişi hazır qeyd edir və məsul aktiv ofisianta bir dəfə bildiriş yaradır.</summary>
    /// <remarks>Body yoxdur. Yalnız Preparing sifarişi üçün yeni keçid edilir. Status, tarix, əməkdaş, audit və bildiriş eyni transaction-dadır. Təkrar Ready əlavə bildiriş yaratmır. Sifariş növbədə qalır; təqdimetmə, ödəniş və masa bağlanması edilmir. Mövcud göndərilmiş sifarişin işlənməsi müqavilənin bitməsi ilə bloklanmır.</remarks>
    [HttpPost(ApiRoutes.KitchenFlow.MarkReady)]
    [ProducesResponseType(typeof(KitchenOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkReady([FromRoute] int restaurantId, [FromRoute] int orderId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ReadyKitchenOrderCommand(restaurantId, orderId), cancellationToken));
}
