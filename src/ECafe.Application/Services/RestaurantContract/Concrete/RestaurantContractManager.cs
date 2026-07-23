using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
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

        public RestaurantContractManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantContractRepository contractRepository,
            IRestaurantRepository restaurantRepository,
            IMinioService minioService,
            IContractDocumentGenerator contractDocumentGenerator,
            IAuditLogService auditLogService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _contractRepository = contractRepository;
            _restaurantRepository = restaurantRepository;
            _minioService = minioService;
            _contractDocumentGenerator = contractDocumentGenerator;
            _auditLogService = auditLogService;
        }

        public async Task<int> CreateAsync(int restaurantId, CreateRestaurantContractRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (request is null)
                throw new BusinessRuleException("Contract request is required!");

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

        public async Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            var contracts = await _contractRepository.GetByRestaurantAsync(restaurantId);
            return (await Task.WhenAll(contracts.Select(MapToResponseAsync))).ToList();
        }

        public async Task<RestaurantContractResponse> GetActiveAsync(int restaurantId)
        {
            var contract = await _contractRepository.GetActiveByRestaurantAsync(restaurantId);
            if (contract is null)
                throw new BusinessRuleException("Restaurant does not have an active contract!");

            return await MapToResponseAsync(contract);
        }

        public async Task ActivateAsync(int restaurantId, int contractId)
        {
            var contract = await GetTrackedContractAsync(restaurantId, contractId);

            ValidateContractDates(contract.StartDate, contract.EndDate);

            var hasOtherActive = await _contractRepository.CheckExistAsync(x =>
                x.RestaurantId == restaurantId &&
                x.Id != contractId &&
                x.StatusId == ContractStatusId(ContractStatus.Active));

            if (hasOtherActive)
                throw new BusinessRuleException("Restaurant already has an active contract!");

            contract.StatusId = ContractStatusId(ContractStatus.Active);
            contract.SignedAt ??= DateTime.UtcNow;
            contract.SignedByUserId ??= GetCurrentUserId();

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();

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

        public async Task TerminateAsync(int restaurantId, int contractId)
        {
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
                    : $"{contract.SignedByUser.Name} {contract.SignedByUser.Surname}".Trim()
            };
        }

        private static int ContractStatusId(ContractStatus status)
            => ((int)StatusType.Contract * 1000) + (int)status;

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
