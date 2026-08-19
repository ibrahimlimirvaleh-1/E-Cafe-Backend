using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Dates;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.Notification;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.DTOs.Workflow;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class RestaurantContractManager : BaseManager, IRestaurantContractService
    {
        private static readonly JsonSerializerOptions NotificationJsonOptions = new(JsonSerializerDefaults.Web);
        private const string ContractFlowCode = "contract";
        private const string SendForSignatureActionCode = "sendForSignature";
        private const string ApproveActionCode = "approve";
        private const string ActivateActionCode = "activate";
        private const string TerminateActionCode = "terminate";

        private readonly IRestaurantContractRepository _contractRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IContractFileService _contractFileService;
        private readonly IAuditLogService _auditLogService;
        private readonly IUserRestaurantRepository _userRestaurantRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationService _notificationService;
        private readonly IWorkflowActionService _workflowActionService;
        private readonly IEmailOutboxService _emailOutboxService;
        private readonly IFileAccessUrlService _fileAccessUrlService;

        public RestaurantContractManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantContractRepository contractRepository,
            IRestaurantRepository restaurantRepository,
            IContractFileService contractFileService,
            IAuditLogService auditLogService,
            IUserRestaurantRepository userRestaurantRepository,
            IUserRepository userRepository,
            INotificationService notificationService,
            IWorkflowActionService workflowActionService,
            IEmailOutboxService emailOutboxService,
            IFileAccessUrlService fileAccessUrlService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _contractRepository = contractRepository;
            _restaurantRepository = restaurantRepository;
            _contractFileService = contractFileService;
            _auditLogService = auditLogService;
            _userRestaurantRepository = userRestaurantRepository;
            _userRepository = userRepository;
            _notificationService = notificationService;
            _workflowActionService = workflowActionService;
            _emailOutboxService = emailOutboxService;
            _fileAccessUrlService = fileAccessUrlService;
        }

        public async Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (request is null)
                throw new BusinessRuleException("Contract request is required!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            ValidateEditableContractDates(request.StartDate, request.EndDate);
            ValidatePercent(request.CommissionPercent, nameof(request.CommissionPercent));
            await EnsureRestaurantDoesNotHaveActiveContractAsync(restaurantId);

            var restaurant = await _restaurantRepository.Query(x => x.Id == restaurantId)
                .Include(x => x.RestaurantGroup)
                .FirstOrDefaultAsync();
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            var contractNumber = await GenerateContractNumberAsync(restaurantId, request.StartDate);
            var generatedFile = await _contractFileService.GenerateAndUploadAsync(restaurant, contractNumber, request);

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
            ValidateEditableContractDates(request.StartDate, request.EndDate);
            ValidatePercent(request.CommissionPercent, nameof(request.CommissionPercent));

            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            var previousStatusId = contract.StatusId;
            var previousStartDate = contract.StartDate;
            var previousEndDate = contract.EndDate;
            var previousCommissionPercent = contract.CommissionPercent;
            var previousStaffSettlementPeriod = contract.StaffSettlementPeriod;
            var previousPaymentPolicyId = contract.PaymentPolicyId;
            var previousFileId = contract.FileId;
            var draftStatusId = ContractStatusId(ContractStatus.Draft);
            var pendingSignatureStatusId = ContractStatusId(ContractStatus.PendingSignature);

            if (previousStatusId != draftStatusId && previousStatusId != pendingSignatureStatusId)
            {
                throw new BusinessRuleException(GetContractEditBlockedMessage(previousStatusId));
            }

            var shouldResetSignatureFlow = previousStatusId == pendingSignatureStatusId;

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
            contract.File = await _contractFileService.GenerateAndUploadAsync(restaurant, contract.ContractNumber, request);

            if (shouldResetSignatureFlow)
            {
                contract.StatusId = draftStatusId;
                contract.SignedAt = null;
                contract.SignedByUserId = null;
            }

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurantId,
                AuditActions.ContractUpdated,
                new
                {
                    contractId = contract.Id,
                    contract.ContractNumber,
                    changedFields = BuildContractChangeDetails(
                        previousStartDate,
                        contract.StartDate,
                        previousEndDate,
                        contract.EndDate,
                        previousCommissionPercent,
                        contract.CommissionPercent,
                        previousStaffSettlementPeriod,
                        contract.StaffSettlementPeriod,
                        previousPaymentPolicyId,
                        contract.PaymentPolicyId,
                        previousFileId,
                        contract.FileId,
                        previousStatusId,
                        contract.StatusId),
                    previousStatusId,
                    currentStatusId = contract.StatusId,
                    requiresResendForSignature = shouldResetSignatureFlow
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
            var response = new List<RestaurantContractResponse>(contracts.Count);

            foreach (var contract in contracts)
            {
                response.Add(await MapToResponseAsync(contract));
            }

            return response;
        }

        public async Task<PaginatedList<RestaurantContractResponse>> GetPagedByRestaurantAsync(
            int restaurantId,
            RestaurantContractFilterRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            request ??= new RestaurantContractFilterRequest();
            var pagination = PaginationFilterNormalizer.Normalize(
                new PaginationFilter(request.PageNumber, request.PageSize));

            var query = _contractRepository.Query(x => x.RestaurantId == restaurantId)
                .Include(x => x.Status)
                .Include(x => x.File)
                .Include(x => x.SignedByUser)
                .AsQueryable();

            if (request.StatusId.HasValue && request.StatusId.Value > 0)
                query = query.Where(x => x.StatusId == request.StatusId.Value);

            var search = NormalizeSearch(request.Search);
            if (search is not null)
            {
                query = query.Where(x =>
                    x.ContractNumber.ToLower().Contains(search) ||
                    (x.Status != null && x.Status.Name.ToLower().Contains(search)));
            }

            if (request.EndDateFrom.HasValue)
            {
                var endDateFromUtc = DateTimeRangeNormalizer.ToUtcRangeStart(request.EndDateFrom.Value);
                query = query.Where(x => x.EndDate.HasValue && x.EndDate.Value >= endDateFromUtc);
            }

            if (request.EndDateTo.HasValue)
            {
                var endDateToUtc = DateTimeRangeNormalizer.ToUtcRangeEnd(request.EndDateTo.Value);
                query = query.Where(x => x.EndDate.HasValue && x.EndDate.Value <= endDateToUtc);
            }

            if (request.ExpiringInDays.HasValue && request.ExpiringInDays.Value > 0)
            {
                var nowUtc = DateTime.UtcNow;
                var limitUtc = nowUtc.AddDays(request.ExpiringInDays.Value);
                query = query.Where(x =>
                    x.EndDate.HasValue &&
                    x.EndDate.Value >= nowUtc &&
                    x.EndDate.Value <= limitUtc);
            }

            var totalCount = await query.CountAsync();
            var contracts = await query
                .OrderByDescending(x => x.StatusId == ContractStatusId(ContractStatus.Active))
                .ThenBy(x => x.EndDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.Id)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            var items = new List<RestaurantContractResponse>(contracts.Count);
            foreach (var contract in contracts)
            {
                items.Add(await MapToResponseAsync(contract));
            }

            return new PaginatedList<RestaurantContractResponse>(
                items,
                totalCount,
                pagination.PageNumber,
                pagination.PageSize);
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

            ValidateContractCanEnterActivationFlow(contract.StartDate, contract.EndDate);
            await EnsureWorkflowActionAllowedAsync(contract, ActivateActionCode);

            var nowUtc = DateTime.UtcNow;
            if (contract.StartDate > nowUtc)
            {
                contract.StatusId = ContractStatusId(ContractStatus.Scheduled);

                await _contractRepository.Update(contract);
                await _contractRepository.SaveChangesAsync();

                await _auditLogService.RecordRestaurantActionAsync(
                    restaurantId,
                    AuditActions.ContractScheduled,
                    new
                    {
                        contractId = contract.Id,
                        contract.ContractNumber,
                        contract.StartDate,
                        scheduledAt = nowUtc
                    },
                    AuditEntityTypes.Contract,
                    contract.Id,
                    contract.ContractNumber);

                return;
            }

            await EnsureRestaurantDoesNotHaveAnotherActiveContractAsync(restaurantId, contractId, nowUtc);

            contract.StatusId = ContractStatusId(ContractStatus.Active);
            contract.SignedAt ??= DateTime.UtcNow;
            contract.SignedByUserId ??= GetCurrentUserId();

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            var owner = await GetRestaurantOwnerAsync(restaurantId);
            await EnqueueContractActivatedEmailAsync(owner.User, contract);
            await NotifyOwnerContractActivatedAsync(contract, owner.UserId);

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
            ValidatePersistedContractDates(contract.StartDate, contract.EndDate);
            EnsureContractHasNotExpired(contract.EndDate);
            await EnsureWorkflowActionAllowedAsync(contract, SendForSignatureActionCode);

            var owner = await GetRestaurantOwnerAsync(restaurantId);

            contract.StatusId = ContractStatusId(ContractStatus.PendingSignature);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await EnqueueContractSignatureEmailAsync(owner.User, contract);
            await NotifyOwnerContractPendingApprovalAsync(contract, owner.UserId);

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
            ValidatePersistedContractDates(contract.StartDate, contract.EndDate);
            EnsureContractHasNotExpired(contract.EndDate);
            await EnsureWorkflowActionAllowedAsync(contract, ApproveActionCode);

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
            await EnsureWorkflowActionAllowedAsync(contract, TerminateActionCode);
            var owner = await GetRestaurantOwnerAsync(restaurantId);

            contract.StatusId = ContractStatusId(ContractStatus.Terminated);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

            await NotifyOwnerContractTerminatedAsync(contract, owner.UserId);

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
                await NotifyOwnerContractExpiredAsync(contract);

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

        public async Task<int> ActivateDueScheduledContractsAsync(int batchSize)
        {
            if (batchSize <= 0)
                batchSize = 100;

            var nowUtc = DateTime.UtcNow;
            var scheduledContracts = await _contractRepository.GetScheduledContractsDueForActivationAsync(nowUtc, batchSize);
            var activatedContracts = new List<Domain.Entities.RestaurantContract>();

            foreach (var contract in scheduledContracts)
            {
                if (await HasAnotherActiveContractAsync(contract.RestaurantId, contract.Id, nowUtc))
                    continue;

                contract.StatusId = ContractStatusId(ContractStatus.Active);
                activatedContracts.Add(contract);
                await _contractRepository.Update(contract);
            }

            if (activatedContracts.Count == 0)
                return 0;

            await _contractRepository.SaveChangesAsync();

            foreach (var contract in activatedContracts)
            {
                var owner = await GetRestaurantOwnerAsync(contract.RestaurantId);
                await EnqueueContractActivatedEmailAsync(owner.User, contract);
                await NotifyOwnerContractActivatedAsync(contract, owner.UserId);

                await _auditLogService.RecordRestaurantActionAsync(
                    contract.RestaurantId,
                    AuditActions.ContractActivated,
                    new
                    {
                        contractId = contract.Id,
                        contract.ContractNumber,
                        activatedAt = nowUtc,
                        activatedBy = "system"
                    },
                    AuditEntityTypes.Contract,
                    contract.Id,
                    contract.ContractNumber);
            }

            return activatedContracts.Count;
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
                FileUrl = contract.FileId.HasValue
                    ? _fileAccessUrlService.BuildViewUrl(contract.FileId.Value)
                    : null,
                FileDownloadUrl = contract.FileId.HasValue
                    ? _fileAccessUrlService.BuildDownloadUrl(contract.FileId.Value)
                    : null,
                SignedAt = contract.SignedAt,
                SignedByUserId = contract.SignedByUserId,
                SignedByUserName = contract.SignedByUser is null
                    ? null
                    : $"{contract.SignedByUser.Name} {contract.SignedByUser.Surname}".Trim(),
                AvailableActions = await ResolveAvailableActionsAsync(contract)
            };
        }

        private static string? NormalizeSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToLower();
        }

        private Task<List<WorkflowActionResponse>> ResolveAvailableActionsAsync(Domain.Entities.RestaurantContract contract)
            => _workflowActionService.GetAvailableActionsAsync(
                ContractFlowCode,
                contract.StatusId,
                contract.RestaurantId,
                contract.Id);

        private Task EnsureWorkflowActionAllowedAsync(Domain.Entities.RestaurantContract contract, string actionCode)
            => _workflowActionService.EnsureCanExecuteAsync(
                ContractFlowCode,
                contract.StatusId,
                actionCode,
                contract.RestaurantId,
                contract.Id);

        private async Task EnsureRestaurantDoesNotHaveActiveContractAsync(int restaurantId)
        {
            if (await _contractRepository.HasActiveContractAsync(restaurantId))
                throw new BusinessRuleException("Restaurant already has an active contract. Terminate or expire the current contract before creating a new one.");
        }

        private async Task EnsureRestaurantDoesNotHaveAnotherActiveContractAsync(int restaurantId, int contractId, DateTime nowUtc)
        {
            if (await HasAnotherActiveContractAsync(restaurantId, contractId, nowUtc))
                throw new BusinessRuleException("Restaurant already has an active contract!");
        }

        private Task<bool> HasAnotherActiveContractAsync(int restaurantId, int contractId, DateTime nowUtc)
            => _contractRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.Id != contractId &&
                x.StatusId == ContractStatusId(ContractStatus.Active) &&
                (!x.EndDate.HasValue || x.EndDate >= nowUtc));

        private static int ContractStatusId(ContractStatus status)
            => ((int)StatusType.Contract * 1000) + (int)status;
        private static string GetContractEditBlockedMessage(int statusId)
        {
            if (statusId == ContractStatusId(ContractStatus.OwnerApproved))
                return "Owner-approved contracts cannot be edited. Create a new draft contract if terms changed.";

            if (statusId == ContractStatusId(ContractStatus.Scheduled))
                return "Scheduled contracts cannot be edited. Use a dedicated reschedule flow before the start date.";

            if (statusId == ContractStatusId(ContractStatus.Active))
                return "Active contracts cannot be edited.";

            if (statusId == ContractStatusId(ContractStatus.Expired))
                return "Expired contracts cannot be edited.";

            if (statusId == ContractStatusId(ContractStatus.Terminated))
                return "Terminated contracts cannot be edited.";

            return "Only draft contracts can be edited. Contracts waiting for signature are returned to draft before resend.";
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

        private Task EnqueueContractSignatureEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailOutboxService.EnqueueContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə təsdiqi gözləyir",
                $"Restoranınız üçün {contract.ContractNumber} nömrəli müqavilə hazırlanıb. Zəhmət olmasa sistemə daxil olub müqaviləni oxuyun və təsdiqləyin.",
                contract.Id);

        private Task EnqueueContractActivatedEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailOutboxService.EnqueueContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə aktivləşdirildi",
                $"{contract.ContractNumber} nömrəli müqaviləniz aktivləşdirildi.",
                contract.Id);

        private Task EnqueueContractOwnerApprovedEmailAsync(
            Domain.Entities.User admin,
            Domain.Entities.RestaurantContract contract,
            string subject,
            string body)
            => _emailOutboxService.EnqueueContractNotificationAsync(
                admin.Email,
                admin.Name,
                subject,
                body,
                contract.Id);

        private Task SendContractSignatureEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailOutboxService.EnqueueContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə təsdiqi gözləyir",
                $"Restoranınız üçün {contract.ContractNumber} nömrəli müqavilə hazırlanıb. Zəhmət olmasa sistemə daxil olub müqaviləni oxuyun və təsdiqləyin.",
                contract.Id);

        private Task SendContractActivatedEmailAsync(
            Domain.Entities.User owner,
            Domain.Entities.RestaurantContract contract)
            => _emailOutboxService.EnqueueContractNotificationAsync(
                owner.Email,
                owner.Name,
                "Müqavilə aktivləşdirildi",
                $"{contract.ContractNumber} nömrəli müqaviləniz aktivləşdirildi.",
                contract.Id);

        private Task NotifyOwnerContractPendingApprovalAsync(
            Domain.Entities.RestaurantContract contract,
            int ownerUserId)
            => CreateContractNotificationAsync(
                ownerUserId,
                contract,
                NotificationType.ContractPendingApproval,
                "Müqavilə təsdiqinizi gözləyir",
                $"{contract.ContractNumber} nömrəli müqavilə təsdiq üçün sizə göndərildi.");

        private Task NotifyOwnerContractActivatedAsync(
            Domain.Entities.RestaurantContract contract,
            int ownerUserId)
            => CreateContractNotificationAsync(
                ownerUserId,
                contract,
                NotificationType.ContractActivated,
                "Müqavilə aktivləşdirildi",
                $"{contract.ContractNumber} nömrəli müqaviləniz aktivləşdirildi.");

        private Task NotifyOwnerContractTerminatedAsync(
            Domain.Entities.RestaurantContract contract,
            int ownerUserId)
            => CreateContractNotificationAsync(
                ownerUserId,
                contract,
                NotificationType.ContractTerminated,
                "Müqavilə ləğv edildi",
                $"{contract.ContractNumber} nömrəli müqaviləniz ləğv edildi.");

        private async Task NotifyOwnerContractExpiredAsync(Domain.Entities.RestaurantContract contract)
        {
            var owner = await _userRestaurantRepository.GetActiveOwnerByRestaurantAsync(contract.RestaurantId);
            if (owner is null)
                return;

            await CreateContractNotificationAsync(
                owner.UserId,
                contract,
                NotificationType.ContractExpired,
                "Müqavilənin müddəti bitdi",
                $"{contract.ContractNumber} nömrəli müqavilənizin müddəti bitdi.");
        }

        private async Task NotifyAdminsContractOwnerApprovedAsync(
            Domain.Entities.RestaurantContract contract,
            Domain.Entities.User owner,
            string acceptanceText)
        {
            var admins = await _userRepository.GetActiveUsersByRoleAsync((int)RoleCode.SuperAdmin);
            if (admins.Count == 0)
                return;

            var title = "Müqavilə sahibkar tərəfindən təsdiqləndi";
            var message = LimitNotificationMessage(
                $"{contract.ContractNumber} nömrəli müqavilə {owner.Name} {owner.Surname} tərəfindən təsdiqləndi. Təsdiq mətni: {acceptanceText}");

            foreach (var admin in admins)
            {
                await CreateContractNotificationAsync(
                    admin.Id,
                    contract,
                    NotificationType.ContractOwnerApproved,
                    title,
                    message);

                await EnqueueContractOwnerApprovedEmailAsync(
                    admin,
                    contract,
                    title,
                    message);
            }
        }

        private Task CreateContractNotificationAsync(
            int userId,
            Domain.Entities.RestaurantContract contract,
            NotificationType type,
            string title,
            string message)
            => _notificationService.CreateAsync(new CreateNotificationRequest
            {
                UserId = userId,
                RestaurantId = contract.RestaurantId,
                Title = title,
                Message = LimitNotificationMessage(message),
                TypeId = (int)type,
                ChannelId = (int)NotificationChannel.InApp,
                PayloadJson = BuildContractNotificationPayload(contract),
                RelatedEntityType = AuditEntityTypes.Contract,
                RelatedEntityId = contract.Id
            });

        private static string BuildContractNotificationPayload(Domain.Entities.RestaurantContract contract)
            => JsonSerializer.Serialize(new
            {
                restaurantId = contract.RestaurantId,
                contractId = contract.Id,
                contractNumber = contract.ContractNumber
            }, NotificationJsonOptions);

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

        private static void ValidateEditableContractDates(DateTime startDate, DateTime? endDate)
        {
            ValidateContractDateRange(startDate, endDate);

            if (startDate.Date < DateTime.UtcNow.Date)
                throw new BusinessRuleException("Contract start date cannot be in the past.");
        }

        private static void ValidatePersistedContractDates(DateTime startDate, DateTime? endDate)
            => ValidateContractDateRange(startDate, endDate);

        private static void ValidateContractCanEnterActivationFlow(DateTime startDate, DateTime? endDate)
        {
            ValidateContractDateRange(startDate, endDate);

            if (endDate!.Value <= DateTime.UtcNow)
                throw new BusinessRuleException("Expired contract cannot be activated.");
        }

        private static void ValidateContractDateRange(DateTime startDate, DateTime? endDate)
        {
            if (startDate == default)
                throw new BusinessRuleException("Contract start date is required!");

            if (!endDate.HasValue)
                throw new BusinessRuleException("Contract end date is required!");

            if (endDate.Value <= startDate)
                throw new BusinessRuleException("Contract end date must be later than start date!");
        }

        private static void EnsureContractHasNotExpired(DateTime? endDate)
        {
            if (!endDate.HasValue)
                throw new BusinessRuleException("Contract end date is required!");

            if (endDate.Value <= DateTime.UtcNow)
                throw new BusinessRuleException("Expired contract cannot continue in the approval flow.");
        }

        private static void ValidatePercent(decimal? value, string fieldName)
        {
            if (value is < 0 or > 100)
                throw new BusinessRuleException($"{fieldName} must be between 0 and 100!");
        }

        private static List<AuditChangedField> BuildContractChangeDetails(
            DateTime previousStartDate,
            DateTime currentStartDate,
            DateTime? previousEndDate,
            DateTime? currentEndDate,
            decimal? previousCommissionPercent,
            decimal? currentCommissionPercent,
            int? previousStaffSettlementPeriod,
            int? currentStaffSettlementPeriod,
            int previousPaymentPolicyId,
            int currentPaymentPolicyId,
            int? previousFileId,
            int? currentFileId,
            int previousStatusId,
            int currentStatusId)
        {
            var changes = new List<AuditChangedField>();

            AddChange(changes, "Başlama tarixi", previousStartDate, currentStartDate);
            AddChange(changes, "Bitmə tarixi", previousEndDate, currentEndDate);
            AddChange(changes, "Komissiya faizi", previousCommissionPercent, currentCommissionPercent);
            AddChange(changes, "Hesablaşma dövrü", previousStaffSettlementPeriod, currentStaffSettlementPeriod);
            AddChange(changes, "Ödəniş siyasəti", previousPaymentPolicyId, currentPaymentPolicyId);
            AddChange(changes, "Müqavilə faylı", previousFileId, currentFileId);
            AddChange(changes, "Status", previousStatusId, currentStatusId);

            return changes;
        }

        private static void AddChange<T>(List<AuditChangedField> changes, string field, T oldValue, T newValue)
        {
            if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
                return;

            changes.Add(new AuditChangedField(
                field,
                FormatAuditValue(oldValue),
                FormatAuditValue(newValue)));
        }

        private static string? FormatAuditValue<T>(T value)
        {
            if (value is null)
                return null;

            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                decimal decimalValue => decimalValue.ToString("0.##"),
                _ => value.ToString()
            };
        }

        private sealed record AuditChangedField(string Field, string? OldValue, string? NewValue);

    }
}
