using ECafe.Application.DTOs.Order;
using ECafe.Domain.Entities;
using static ECafe.Application.Mappings.OrderMappingValues;

namespace ECafe.Application.Mappings;

public static class OrderCancellationRequestMapper
{
    /// <summary>Yalnız sorğunun səbəb və tarixçəsini çıxarır; daxili əməkdaş ID-lərini müştəri görünüşünə buraxmır.</summary>
    public static OrderCancellationRequestResponse? Map(OrderCancellationRequest? request)
        => request == null ? null : new(request.Id, (int)request.StatusId, request.Reason, AsUtc(request.RequestedAt),
            AsNullableUtc(request.ForwardedAt), AsNullableUtc(request.ResolvedAt),
            request.StatusId == Domain.Enums.OrderCancellationRequestStatus.Expired ? null : request.DecisionReason);
}
