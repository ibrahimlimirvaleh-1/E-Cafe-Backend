using ECafe.Application.DTOs.Lookup;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECafe.Api.Controllers
{
    [Authorize]
    public class LookupController : BaseController
    {
        [HttpGet("api/v1/lookups/roles")]
        public IActionResult GetRoles()
            => Ok(MapEnum<RoleCode>());

        [HttpGet("api/v1/lookups/item-statuses")]
        public IActionResult GetItemStatuses()
            => Ok(MapStatusEnum<ItemStatus>(StatusType.ItemStatus));

        [HttpGet("api/v1/lookups/contract-statuses")]
        public IActionResult GetContractStatuses()
            => Ok(MapStatusEnum<ContractStatus>(StatusType.Contract));

        [HttpGet("api/v1/lookups/payment-policies")]
        public IActionResult GetPaymentPolicies()
            => Ok(MapEnum<ContractPaymentPolicy>());

        private static List<LookupItemResponse> MapEnum<TEnum>()
            where TEnum : struct, Enum
            => Enum.GetValues<TEnum>()
                .Select(value => new LookupItemResponse
                {
                    Id = Convert.ToInt32(value),
                    Code = value.ToString(),
                    Name = value.GetDescription()
                })
                .ToList();

        private static List<LookupItemResponse> MapStatusEnum<TEnum>(StatusType statusType)
            where TEnum : struct, Enum
        {
            var statusTypeId = (int)statusType * 1000;

            return Enum.GetValues<TEnum>()
                .Select(value => new LookupItemResponse
                {
                    Id = statusTypeId + Convert.ToInt32(value),
                    Code = value.ToString(),
                    Name = value.GetDescription()
                })
                .ToList();
        }
    }
}
