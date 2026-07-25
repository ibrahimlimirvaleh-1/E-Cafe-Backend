using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class RestaurantContractManager : BaseManager, IRestaurantContractService
    {
        private readonly IRestaurantContractRepository _contractRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IMinioService _minioService;
        private readonly IContractDocumentGenerator _contractDocumentGenerator;
        private readonly IAuditLogService _auditLogService;
        private readonly IUserRestaurantRepository _userRestaurantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IBaseRepository<Domain.Entities.Notification> _notificationRepository;
        private readonly IBaseRepository<Domain.Entities.WorkflowActionRule> _workflowActionRuleRepository;
        private readonly IEmailService _emailService;

        public RestaurantContractManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantContractRepository contractRepository,
            IRestaurantRepository restaurantRepository,
            IMinioService minioService,
            IContractDocumentGenerator contractDocumentGenerator,
            IAuditLogService auditLogService,
            IUserRestaurantRepository userRestaurantRepository,
            IUserRepository userRepository,
            IBaseRepository<Domain.Entities.Notification> notificationRepository,
            IBaseRepository<Domain.Entities.WorkflowActionRule> workflowActionRuleRepository,
            IEmailService emailService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _contractRepository = contractRepository;
            _restaurantRepository = restaurantRepository;
            _minioService = minioService;
            _contractDocumentGenerator = contractDocumentGenerator;
            _auditLogService = auditLogService;
            _userRestaurantRepository = userRestaurantRepository;
            _userRepository = userRepository;
            _notificationRepository = notificationRepository;
            _workflowActionRuleRepository = workflowActionRuleRepository;
            _emailService = emailService;
        }

        public async Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (request is null)
                throw new BusinessRuleException("Contract request is required!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            ValidateContractDates(request.StartDate, request.EndDate);
            ValidatePercent(request.CommissionPercent, nameof(request.CommissionPercent));

            var restaurant = await _restaurantRepository.Query(x => x.Id == restaurantId)
                .Include(x => x.RestaurantGroup)
                .FirstOrDefaultAsync();
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            var contractNumber = await GenerateContractNumberAsync(restaurantId, request.StartDate);
            var generatedFile = await ResolveContractFileAsync(restaurant, contractNumber, request);

            var contract = new Domain.Entities.RestaurantContract
            {
                RestaurantId = restaurantId,
                ContractNumber = contractNumber,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CommissionPercent = request.CommissionPercent,
                StaffSettlementPeriod = request.StaffSettlementPeriod,
                PaymentPolicyId = request.PaymentPolicyId,
                StatusId = ContractStatusId(ContractStatus.Draft),
                File = generatedFile
            };

            await _contractRepository.Add(contract);
            await _contractRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractCreated,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    contract.StartDate,
                    contract.EndDate,
                    contract.CommissionPercent,
                    contract.StaffSettlementPeriod,
                    contract.PaymentPolicyId,
                    contract.FileId
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);

            return contract.Id;
        }

        public async Task UpdateAsync(int restaurantId, int contractId, UpdateRestaurantContractRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (contractId <= 0)
                throw new BusinessRuleException("Invalid contract ID!");

            if (request is null)
                throw new BusinessRuleException("Contract request is required!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);
            ValidateContractDates(request.StartDate, request.EndDate);
            ValidatePercent(request.CommissionPercent, nameof(request.CommissionPercent));

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            if (contract.StatusId is var statusId &&
                (statusId == ContractStatusId(ContractStatus.Active) ||
                 statusId == ContractStatusId(ContractStatus.Expired) ||
                 statusId == ContractStatusId(ContractStatus.Terminated)))
            {
                throw new BusinessRuleException("Only non-active contracts can be edited.");
            }

            var restaurant = await _restaurantRepository.Query(x => x.Id == restaurantId)
                .Include(x => x.RestaurantGroup)
                .FirstOrDefaultAsync();
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            contract.StartDate = request.StartDate;
            contract.EndDate = request.EndDate;
            contract.CommissionPercent = request.CommissionPercent;
            contract.StaffSettlementPeriod = request.StaffSettlementPeriod;
            contract.PaymentPolicyId = request.PaymentPolicyId;
            contract.File = await ResolveContractFileAsync(restaurant, contract.ContractNumber, request);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractUpdated,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    contract.StartDate,
                    contract.EndDate,
                    contract.CommissionPercent,
                    contract.StaffSettlementPeriod,
                    contract.PaymentPolicyId,
                    contract.FileId
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);
        }

        public async Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contracts = await _contractRepository.GetByRestaurantAsync(restaurantId);
            return (await Task.WhenAll(contracts.Select(MapToResponseAsync))).ToList();
        }

        public async Task<RestaurantContractResponse> GetActiveAsync(int restaurantId)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await _contractRepository.GetActiveByRestaurantAsync(restaurantId);
            if (contract is null)
                throw new BusinessRuleException("Restaurant does not have an active contract!");

            return await MapToResponseAsync(contract);
        }

        public async Task<List<WorkflowActionResponse>> GetAvailableActionsAsync(int restaurantId, int contractId)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            return await ResolveAvailableActionsAsync(contract);
        }

        public async Task ActivateAsync(int restaurantId, int contractId)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await GetTrackedContractAsync(restaurantId, contractId);

            ValidateContractDates(contract.StartDate, contract.EndDate);
            EnsureContractHasNotExpired(contract.EndDate);
            EnsureContractStatus(contract, ContractStatus.OwnerApproved, "Only owner-approved contracts can be activated.");

            var nowUtc = DateTime.UtcNow;
            var hasOtherActive = await _contractRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.Id != contractId &&
                x.StatusId == ContractStatusId(ContractStatus.Active) &&
                (!x.EndDate.HasValue || x.EndDate >= nowUtc));

            if (hasOtherActive)
                throw new BusinessRuleException("Restaurant already has an active contract!");

            contract.StatusId = ContractStatusId(ContractStatus.Active);
            contract.SignedAt ??= DateTime.UtcNow;
            contract.SignedByUserId ??= GetCurrentUserId();

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            var owner = await GetRestaurantOwnerAsync(restaurantId);
            await SendContractActivatedEmailAsync(owner.User, contract);

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractActivated,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    contract.SignedAt,
                    contract.SignedByUserId
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);
        }

        public async Task SendForSignatureAsync(int restaurantId, int contractId)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            ValidateContractDates(contract.StartDate, contract.EndDate);
            EnsureContractHasNotExpired(contract.EndDate);
            EnsureContractStatus(contract, ContractStatus.Draft, "Only draft contracts can be sent for signature.");

            var owner = await GetRestaurantOwnerAsync(restaurantId);

            contract.StatusId = ContractStatusId(ContractStatus.PendingSignature);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await SendContractSignatureEmailAsync(owner.User, contract);

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractSentForSignature,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    ownerUserId = owner.UserId
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);
        }

        public async Task ApproveAsync(
            int restaurantId,
            int contractId,
            bool hasAcceptedContractTerms,
            string? acceptanceText)
        {
            EnsureOwnerAcceptedContractTerms(hasAcceptedContractTerms, acceptanceText);
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            ValidateContractDates(contract.StartDate, contract.EndDate);
            EnsureContractHasNotExpired(contract.EndDate);
            EnsureContractStatus(contract, ContractStatus.PendingSignature, "Only contracts waiting for signature can be approved.");

            var owner = await GetRestaurantOwnerAsync(restaurantId);
            var currentUserId = GetCurrentUserId();
            if (owner.UserId != currentUserId)
                throw new BusinessRuleException("Only the restaurant owner can approve this contract.");

            contract.StatusId = ContractStatusId(ContractStatus.OwnerApproved);
            contract.SignedAt = DateTime.UtcNow;
            contract.SignedByUserId = currentUserId;

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await NotifyAdminsContractOwnerApprovedAsync(contract, owner.User, acceptanceText!);

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractOwnerApproved,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    contract.SignedAt,
                    contract.SignedByUserId,
                    acceptanceText
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);
        }

        public async Task TerminateAsync(int restaurantId, int contractId)
        {
            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            contract.StatusId = ContractStatusId(ContractStatus.Terminated);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractTerminated,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    contract.StatusId
                },
                AuditEntityTypes.Contract,
                contract.Id,
                contract.ContractNumber);
        }

        public async Task EnsureRestaurantHasActiveContractAsync(int restaurantId)
        {
            if (!await _contractRepository.HasActiveContractAsync(restaurantId))
                throw new BusinessRuleException("Restaurant does not have an active contract!");
        }

        public async Task<int> ExpireActiveContractsAsync(int batchSize)
        {
            if (batchSize <= 0)
                batchSize = 100;

            var nowUtc = DateTime.UtcNow;
            var expiredContracts = await _contractRepository.GetExpiredActiveContractsAsync(nowUtc, batchSize);

            foreach (var contract in expiredContracts)
            {
                contract.StatusId = ContractStatusId(ContractStatus.Expired);
                await _contractRepository.Update(contract);
            }

            if (expiredContracts.Count == 0)
                return 0;

            await _contractRepository.SaveChangesAsync();

            foreach (var contract in expiredContracts)
            {
                await _auditLogService.RecordRestaurantActionAsync(
                    contract.RestaurantId,
                    AuditActions.ContractExpired,
                    new
                    {
                        contractId = contract.Id,
                        contract.ContractNumber,
                        contract.EndDate,
                        expiredAt = nowUtc
                    },
                    AuditEntityTypes.Contract,
                    contract.Id,
                    contract.ContractNumber);
            }

            return expiredContracts.Count;
        }

        private async Task<Domain.Entities.RestaurantContract> GetTrackedContractAsync(int restaurantId, int contractId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (contractId <= 0)
                throw new BusinessRuleException("Invalid contract ID!");

            var contract = await _contractRepository.GetTrackedByRestaurantAsync(restaurantId, contractId);
            if (contract is null)
                throw new BusinessRuleException("Restaurant contract not found!");

            return contract;
        }

        private async Task<RestaurantContractResponse> MapToResponseAsync(Domain.Entities.RestaurantContract contract)
        {
            var fileUrl = contract.File is null
                ? null
                : await _minioService.GenerateFileUrl(contract.File.Token);

            return new RestaurantContractResponse
            {
                Id = contract.Id,
                RestaurantId = contract.RestaurantId,
                ContractNumber = contract.ContractNumber,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                CommissionPercent = contract.CommissionPercent,
                StaffSettlementPeriod = contract.StaffSettlementPeriod,
                PaymentPolicyId = contract.PaymentPolicyId,
                StatusId = contract.StatusId,
                StatusName = contract.Status?.Name,
                FileId = contract.FileId,
                FileUrl = fileUrl,
                SignedAt = contract.SignedAt,
                SignedByUserId = contract.SignedByUserId,
                SignedByUserName = contract.SignedByUser is null
                    ? null
                    : $"{contract.SignedByUser.Name} {contract.SignedByUser.Surname}".Trim(),
                AvailableActions = await ResolveAvailableActionsAsync(contract)
            };
        }

        private async Task<List<WorkflowActionResponse>> ResolveAvailableActionsAsync(Domain.Entities.RestaurantContract contract)
        {
            var roleId = GetCurrentRoleId();
            var rules = await _workflowActionRuleRepository.Query(rule =>
                    rule.FlowCode == "contract" &&
                    rule.StatusId == contract.StatusId &&
                    rule.RoleId == roleId &&
                    rule.IsEnabled)
                .OrderBy(rule => rule.SortOrder)
                .ThenBy(rule => rule.Id)
                .ToListAsync();

            if (rules.Count == 0)
                return [];

            if (roleId == (int)RoleCode.Owner && !await IsCurrentUserRestaurantOwnerAsync(contract.RestaurantId))
                return [];

            return rules
                .Select(rule => new WorkflowActionResponse
                {
                    Code = rule.ActionCode,
                    Label = rule.Label,
                    HttpMethod = rule.HttpMethod,
                    Endpoint = BuildActionEndpoint(rule.EndpointTemplate, contract.RestaurantId, contract.Id),
                    RequiresConfirmation = rule.RequiresConfirmation,
                    SortOrder = rule.SortOrder
                })
                .ToList();
        }

        private async Task<bool> IsCurrentUserRestaurantOwnerAsync(int restaurantId)
        {
            var owner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(restaurantId);
            return owner?.UserId == GetCurrentUserId();
        }

        private static string BuildActionEndpoint(string template, int restaurantId, int contractId)
            => template
                .Replace("{restaurantId}", restaurantId.ToString())
                .Replace("{contractId}", contractId.ToString());

        private static int ContractStatusId(ContractStatus status)
            => ((int)StatusType.Contract * 1000) + (int)status;

        private static void EnsureContractStatus(
            Domain.Entities.RestaurantContract contract,
            ContractStatus expectedStatus,
            string message)
        {
            if (contract.StatusId != ContractStatusId(expectedStatus))
                throw new BusinessRuleException(message);
        }

        private static void EnsureOwnerAcceptedContractTerms(bool hasAcceptedContractTerms, string? acceptanceText)
        {
            if (!hasAcceptedContractTerms)
                throw new BusinessRuleException("Contract terms must be accepted.");

            if (string.IsNullOrWhiteSpace(acceptanceText))
                throw new BusinessRuleException("Contract acceptance text is required.");
        }

        private async Task<Domain.Entities.UserRestaurant> GetRestaurantOwnerAsync(int restaurantId)
        {
            var owner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(restaurantId);
            if (owner is null)
                throw new BusinessRuleException("Restaurant owner is not assigned.");

            return owner;
        }

        private Task SendContractSignatureEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailService.SendContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə təsdiqi gözləyir",
                $"Restoranınız üçün {contract.ContractNumber} nömrəli müqavilə hazırlanıb. Zəhmət olmasa sistemə daxil olub müqaviləni oxuyun və təsdiqləyin.");

        private Task SendContractActivatedEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailService.SendContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə aktivləşdirildi",
                $"{contract.ContractNumber} nömrəli müqaviləniz aktivləşdirildi.");

        private async Task NotifyAdminsContractOwnerApprovedAsync(
            Domain.Entities.RestaurantContract contract,
            Domain.Entities.User owner,
            string acceptanceText)
        {
            var admins = await _userRepository.GetActiveUsersByRoleAsync((int)RoleCode.SuperAdmin);
            if (admins.Count == 0)
                return;

            var title = "Müqavilə owner tərəfindən təsdiqləndi";
            var message = LimitNotificationMessage(
                $"{contract.ContractNumber} nömrəli müqavilə {owner.Name} {owner.Surname} tərəfindən təsdiqləndi. Təsdiq mətni: {acceptanceText}");

            var notifications = admins.Select(admin => new Domain.Entities.Notification
            {
                UserId = admin.Id,
                Title = title,
                Message = message,
                IsRead = false
            }).ToList();

            await _notificationRepository.AddRangeAsync(notifications);
            await _notificationRepository.SaveChangesAsync();

            foreach (var admin in admins)
            {
                await _emailService.SendContractNotificationAsync(
                    admin.Email,
                    admin.Name,
                    title,
                    message);
            }
        }

        private static string LimitNotificationMessage(string message)
            => message.Length <= 1000 ? message : message[..1000];

        private async Task<string> GenerateContractNumberAsync(int restaurantId, DateTime startDate)
        {
            var year = startDate.Year;
            var prefix = $"EC-{year}-{restaurantId:D5}-";
            var sequence = await _contractRepository.Query(x => x.ContractNumber.StartsWith(prefix))
                .CountAsync() + 1;

            for (var attempt = 0; attempt < 100; attempt++)
            {
                var contractNumber = $"{prefix}{sequence + attempt:D5}";
                var exists = await _contractRepository.CheckExistAsync(x => x.ContractNumber == contractNumber);

                if (!exists)
                    return contractNumber;
            }

            throw new BusinessRuleException("Could not generate a unique contract number.");
        }

        private static void ValidateContractDates(DateTime startDate, DateTime? endDate)
        {
            if (startDate == default)
                throw new BusinessRuleException("Contract start date is required!");

            if (endDate.HasValue && endDate.Value < startDate)
                throw new BusinessRuleException("Contract end date cannot be earlier than start date!");
        }

        private static void EnsureContractHasNotExpired(DateTime? endDate)
        {
            if (endDate.HasValue && endDate.Value < DateTime.UtcNow)
                throw new BusinessRuleException("Expired contract cannot be activated.");
        }

        private static void ValidatePercent(decimal? value, string fieldName)
        {
            if (value is < 0 or > 100)
                throw new BusinessRuleException($"{fieldName} must be between 0 and 100!");
        }

        private async Task<File> ResolveContractFileAsync(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            var document = _contractDocumentGenerator.Generate(BuildContractDocumentData(
                restaurant,
                contractNumber,
                request));

            var token = await _minioService.UploadFileAsync(new UploadGeneratedFileDto
            {
                Bytes = document.Bytes,
                FileName = document.FileName,
                ContentType = document.ContentType
            });

            return Mapper.Map<File>(new FileMapData
            {
                Token = token,
                FileName = document.FileName,
                Size = document.Bytes.LongLength,
                Url = await _minioService.GenerateFileUrl(token)
            });
        }

        private static RestaurantContractDocumentData BuildContractDocumentData(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            var legalName = string.IsNullOrWhiteSpace(restaurant.RestaurantGroup?.LegalName)
                ? restaurant.Name
                : restaurant.RestaurantGroup.LegalName;
            var branchName = string.IsNullOrWhiteSpace(restaurant.BranchName)
                ? restaurant.Name
                : restaurant.BranchName;

            return new RestaurantContractDocumentData
            {
                ContractNumber = contractNumber,
                RestaurantName = restaurant.Name,
                LegalName = legalName,
                BranchName = branchName,
                Location = restaurant.Location,
                Phone = restaurant.Phone,
                Email = restaurant.Email,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CommissionPercent = request.CommissionPercent,
                StaffSettlementPeriod = request.StaffSettlementPeriod,
                PaymentPolicyId = request.PaymentPolicyId
            };
        }
    }
}
