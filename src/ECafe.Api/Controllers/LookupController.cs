using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Lookup;
using ECafe.Application.Repositories.Role;
using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Api.Controllers
{
    [Authorize]
    public class LookupController : BaseController
    {
        private readonly IRoleRepository _roleRepository;

        public LookupController(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        [HttpGet("api/v1/lookups/roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleRepository.Query()
                .OrderBy(role => role.Id)
                .ToListAsync();

            return Ok(roles.Select(role => new RoleLookupItemResponse
            {
                Id = role.Id,
                Code = Enum.IsDefined(typeof(RoleCode), role.Id)
                    ? ((RoleCode)role.Id).ToString()
                    : role.Id.ToString(),
                Name = role.Name,
                IsStaffAssignable = role.IsStaffAssignable
            }));
        }

        [HttpGet("api/v1/lookups/item-statuses")]
        public IActionResult GetItemStatuses()
            => Ok(MapStatusEnum<ItemStatus>(StatusType.ItemStatus));

        [HttpGet("api/v1/lookups/contract-statuses")]
        public IActionResult GetContractStatuses()
            => Ok(MapStatusEnum<ContractStatus>(StatusType.Contract));

        [HttpGet("api/v1/lookups/payment-policies")]
        public IActionResult GetPaymentPolicies()
            => Ok(MapEnum<ContractPaymentPolicy>());

        [HttpGet("api/v1/lookups/units")]
        public IActionResult GetUnits()
            => Ok(MapEnum<UnitCode>());

        [HttpGet("api/v1/lookups/actions")]
        [HttpGet("api/v1/lookups/audit-actions")]
        public IActionResult GetAuditActions()
            => Ok(AuditActions.All
                .OrderBy(action => action.Id)
                .Select(action => new LookupItemResponse
                {
                    Id = action.Id,
                    Code = action.Code,
                    Name = action.DisplayName
                })
                .ToList());



        [HttpGet("api/v1/lookups/inventory-movement-types")]
        [HttpGet("api/v1/lookups/getInventoryMovementTypes")]
        public IActionResult GetInventoryMovementTypes()
            => Ok(MapEnum<InventoryMovementTypeCode>());

        [HttpGet("api/v1/lookups/outbox-statuses")]
        public IActionResult GetOutboxStatuses()
            => Ok(MapEnum<OutboxMessageStatus>());

        [HttpGet("api/v1/lookups/notification-channels")]
        public IActionResult GetNotificationChannels()
            => Ok(MapEnum<OutboxMessageChannel>());

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
