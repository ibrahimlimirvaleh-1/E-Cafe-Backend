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
public sealed class KitchenFlowController : BaseController
{
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
