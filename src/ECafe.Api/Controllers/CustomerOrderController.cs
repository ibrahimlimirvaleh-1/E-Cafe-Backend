using ECafe.Application.DTOs.Order;
using ECafe.Application.Features.Queries.Order;
using ECafe.Application.Features.Commands.Order;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Api.Controllers;

[Authorize(Roles = "5")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerOrderController : BaseController
{
    /// <summary>Müştərinin öz açıq sessiyasında ofisiant təsdiqini gözləyən sifariş yaradır; mətbəx, stok və ödəniş dəyişmir.</summary>
    /// <remarks>Idempotency-Key hər yeni sifariş üçün UUID-dir. Cavabı bilinməyən sorğunu eyni açar və məzmunla təkrarlayın. İlk cavab 201, təkrar 200, fərqli məzmunlu eyni açar 409 olur. Müştəri və restoran sorğudan qəbul edilmir; qiymətlər serverdə hesablanır.</remarks>
    [HttpPost(ApiRoutes.CustomerOrder.Create)]
    [ProducesResponseType(typeof(CustomerSessionOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CustomerSessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromRoute] int tableSessionId,
        [FromHeader(Name = "Idempotency-Key")] Guid requestId, [FromBody] CreateCustomerOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateCustomerOrderCommand(tableSessionId, requestId, request), cancellationToken);
        return result.IsReplay ? Ok(result.Order) : CreatedAtAction(nameof(GetOrder),
            new { tableSessionId, orderId = result.Order.Id }, result.Order);
    }

    /// <summary>Müştərinin hazırlanan sifarişi üçün cari ofisianta ləğv istəyi yaradır; başqa şəxsin sessiyasını qəbul etmir.</summary>
    [HttpPost(ApiRoutes.CustomerOrder.RequestCancellation)]
    [ProducesResponseType(typeof(CustomerSessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestCancellation([FromRoute] int tableSessionId, [FromRoute] int orderId,
        [FromBody] CancelOrderRequest request, CancellationToken token)
        => Ok(await Mediator.Send(new RequestCustomerCancellationCommand(tableSessionId, orderId, request), token));

    /// <summary>Müştəri öz sessiyasında hələ hazırlanmağa başlanmamış sifarişi birbaşa ləğv edir.</summary>
    /// <remarks>Body: reason (3-500 simvol); yeni Idempotency-Key lazım deyil. Yalnız PendingWaiterConfirmation/Created/Accepted. Eyni orderId təkrarı ilk nəticəni qaytarır. Yad sessiya/sifariş üçün 404, hazırlama və ödəniş konflikti üçün 409. Stok, depozit və rezervasiya dəyişmir.</remarks>
    [HttpPost(ApiRoutes.CustomerOrder.Cancel)]
    [ProducesResponseType(typeof(CustomerSessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel([FromRoute] int tableSessionId, [FromRoute] int orderId,
        [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new CancelCustomerOrderCommand(tableSessionId, orderId, request), cancellationToken));

    /// <summary>Müştərinin öz açıq və bağlanmış masa sessiyalarını ən yenidən köhnəyə qaytarır.</summary>
    /// <remarks>Yalnız giriş etmiş aktiv müştəri üçün. PageNumber=1, PageSize=20, maksimum 100. Müştəri/restoran ID-si sorğudan alınmır; başqa müştəriyə aid sessiyalar görünmür. Tarixçə müqavilənin və restoranın aktivliyindən asılı deyil.</remarks>
    [HttpGet(ApiRoutes.CustomerOrder.GetMySessions)]
    [ProducesResponseType(typeof(PaginatedList<CustomerTableSessionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySessions([FromQuery] PaginationFilter pagination, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetCustomerTableSessionsQuery(pagination), cancellationToken));

    /// <summary>Müştərinin öz masa sessiyasının restoran, masa və vaxt məlumatlarını qaytarır.</summary>
    /// <remarks>Yad, silinmiş və mövcud olmayan sessiya eyni 404 cavabı alır. Bağlı sessiya tarixçə kimi oxunur; hazırda həmin masada oturan başqa müştərinin məlumatı açılmır. Vaxtlar UTC, TimeZone restoranın saat zonasıdır.</remarks>
    [HttpGet(ApiRoutes.CustomerOrder.GetSession)]
    [ProducesResponseType(typeof(CustomerTableSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession([FromRoute] int tableSessionId, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetCustomerTableSessionQuery(tableSessionId), cancellationToken));

    /// <summary>Müştərinin öz sessiyasındakı bütün sifarişləri, ofisiantın yaratdıqları da daxil olmaqla, səhifələnmiş qaytarır.</summary>
    /// <remarks>Ən yeni sifariş əvvəldədir. Status, faktiki mərhələ vaxtları, saxlanılmış məhsul adları/qiymətləri və məhsul cəmi qaytarılır. Subtotal yekun hesab və ya ödəniş deyil. Daxili qeydlər, işçi ID-ləri və ödəniş rekvizitləri yoxdur. Frontend açıq görünüşdə 30 saniyəlik yeniləmə edə bilər; bu endpoint status dəyişmir və bildiriş yaratmır.</remarks>
    [HttpGet(ApiRoutes.CustomerOrder.GetOrders)]
    [ProducesResponseType(typeof(PaginatedList<CustomerSessionOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders([FromRoute] int tableSessionId, [FromQuery] PaginationFilter pagination,
        CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetCustomerSessionOrdersQuery(tableSessionId, pagination), cancellationToken));

    /// <summary>Yalnız müştərinin öz sessiyasındakı sifarişin izləmə detalını qaytarır.</summary>
    /// <remarks>Başqa sessiyanın və ya müştərinin sifarişi eyni 404 cavabı alır. ID ilə keçid səlahiyyət vermir. Hazırdır təqdim edilib demək deyil; null vaxt hələ qeydə alınmamış mərhələni bildirir. Yaratma, ləğv, servis, stok və ödəniş əməliyyatı yoxdur.</remarks>
    [HttpGet(ApiRoutes.CustomerOrder.GetOrder)]
    [ProducesResponseType(typeof(CustomerSessionOrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrder([FromRoute] int tableSessionId, [FromRoute] int orderId,
        CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetCustomerSessionOrderQuery(tableSessionId, orderId), cancellationToken));
}
