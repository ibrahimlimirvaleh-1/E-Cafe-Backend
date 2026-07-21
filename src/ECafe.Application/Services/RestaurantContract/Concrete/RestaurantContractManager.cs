using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;
using System.Net;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class RestaurantContractManager : BaseManager, IRestaurantContractService
    {
        private readonly IRestaurantContractRepository _contractRepository;
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IFileRepository _fileRepository;
        private readonly IMinioService _minioService;
        private const string ContractContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        public RestaurantContractManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantContractRepository contractRepository,
            IRestaurantRepository restaurantRepository,
            IFileRepository fileRepository,
            IMinioService minioService)
            : base(httpContextAccessor, mapper, configuration)
        {
            _contractRepository = contractRepository;
            _restaurantRepository = restaurantRepository;
            _fileRepository = fileRepository;
            _minioService = minioService;
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
                FileId = request.FileId,
                File = generatedFile
            };

            await _contractRepository.Add(contract);
            await _contractRepository.SaveChangesAsync();

            return contract.Id;
        }

        public async Task<List<RestaurantContractResponse>> GetByRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            var contracts = await _contractRepository.GetByRestaurantAsync(restaurantId);
            return contracts.Select(MapToResponse).ToList();
        }

        public async Task<RestaurantContractResponse> GetActiveAsync(int restaurantId)
        {
            var contract = await _contractRepository.GetActiveByRestaurantAsync(restaurantId);
            if (contract is null)
                throw new BusinessRuleException("Restaurant does not have an active contract!");

            return MapToResponse(contract);
        }

        public async Task ActivateAsync(int restaurantId, int contractId, int? signedByUserId)
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
            contract.SignedByUserId ??= signedByUserId;

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();
        }

        public async Task TerminateAsync(int restaurantId, int contractId)
        {
            var contract = await GetTrackedContractAsync(restaurantId, contractId);
            contract.StatusId = ContractStatusId(ContractStatus.Terminated);

            await _contractRepository.Update(contract);
            await _contractRepository.SaveChangesAsync();
        }

        public async Task EnsureRestaurantHasActiveContractAsync(int restaurantId)
        {
            if (!await _contractRepository.HasActiveContractAsync(restaurantId))
                throw new BusinessRuleException("Restaurant does not have an active contract!");
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

        private static RestaurantContractResponse MapToResponse(Domain.Entities.RestaurantContract contract)
            => new()
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
                FileUrl = contract.File?.Url,
                SignedAt = contract.SignedAt,
                SignedByUserId = contract.SignedByUserId,
                SignedByUserName = contract.SignedByUser is null
                    ? null
                    : $"{contract.SignedByUser.Name} {contract.SignedByUser.Surname}".Trim()
            };

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

        private async Task<File?> ResolveContractFileAsync(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            if (request.FileId.HasValue)
            {
                if (request.FileId.Value <= 0)
                    throw new BusinessRuleException("Invalid contract file ID!");

                var existingFile = await _fileRepository.GetAttachableByIdAsync(request.FileId.Value);
                if (existingFile is null)
                    throw new BusinessRuleException("Contract file not found or already attached!");

                return null;
            }

            var fileName = $"{contractNumber}.docx";
            var bytes = GenerateContractDocx(restaurant, contractNumber, request);
            var token = await _minioService.UploadFileAsync(new UploadGeneratedFileDto
            {
                Bytes = bytes,
                FileName = fileName,
                ContentType = ContractContentType
            });

            return Mapper.Map<File>(new FileMapData
            {
                Token = token,
                FileName = fileName,
                Size = bytes.LongLength,
                Url = await _minioService.GenerateFileUrl(token)
            });
        }

        private static byte[] GenerateContractDocx(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipEntry(archive, "[Content_Types].xml", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                    </Types>
                    """);

                WriteZipEntry(archive, "_rels/.rels", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                    </Relationships>
                    """);

                WriteZipEntry(archive, "word/document.xml", BuildContractDocumentXml(restaurant, contractNumber, request));
            }

            return stream.ToArray();
        }

        private static string BuildContractDocumentXml(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            var restaurantGroup = restaurant.RestaurantGroup;
            var legalName = string.IsNullOrWhiteSpace(restaurantGroup?.LegalName)
                ? restaurant.Name
                : restaurantGroup.LegalName;
            var branchName = string.IsNullOrWhiteSpace(restaurant.BranchName)
                ? restaurant.Name
                : restaurant.BranchName;

            var lines = new[]
            {
                ("E-Cafe restoran xidmətləri müqaviləsi", true),
                ($"Müqavilə nömrəsi: {contractNumber}", false),
                ($"Restoran: {restaurant.Name}", false),
                ($"Hüquqi ad: {legalName}", false),
                ($"Filial: {branchName}", false),
                ($"Ünvan: {restaurant.Location}", false),
                ($"Telefon: {restaurant.Phone}", false),
                ($"Email: {restaurant.Email}", false),
                ($"Başlama tarixi: {FormatDate(request.StartDate)}", false),
                ($"Bitmə tarixi: {FormatDate(request.EndDate)}", false),
                ($"Komissiya faizi: {FormatPercent(request.CommissionPercent)}", false),
                ($"İşçi hesablaşma periodu: {FormatSettlementPeriod(request.StaffSettlementPeriod)}", false),
                ("", false),
                ("Tərəflər bu müqavilə ilə E-Cafe platforması üzərindən restoran xidmətlərinin göstərilməsi, sifarişlərin idarə olunması və ödənişlərin emalı şərtlərini qəbul edirlər.", false),
                ("", false),
                ("Platforma nümayəndəsi: ____________________", false),
                ("Restoran nümayəndəsi: ____________________", false)
            };

            var paragraphs = string.Concat(lines.Select(line => BuildParagraph(line.Item1, line.Item2)));

            return $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    {{paragraphs}}
                    <w:sectPr>
                      <w:pgSz w:w="11906" w:h="16838"/>
                      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/>
                    </w:sectPr>
                  </w:body>
                </w:document>
                """;
        }

        private static string BuildParagraph(string text, bool bold)
        {
            var runProperties = bold ? "<w:rPr><w:b/></w:rPr>" : string.Empty;
            return $"<w:p><w:r>{runProperties}<w:t>{WebUtility.HtmlEncode(text)}</w:t></w:r></w:p>";
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        private static string FormatDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";

        private static string FormatPercent(decimal? value)
            => value.HasValue ? $"{value:0.##}%" : "-";

        private static string FormatSettlementPeriod(int? value)
            => value.HasValue ? $"{value} gün" : "-";
    }
}
