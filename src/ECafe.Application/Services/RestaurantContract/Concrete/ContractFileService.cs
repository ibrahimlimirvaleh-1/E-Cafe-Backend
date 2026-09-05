using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.Repositories.FileType;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.FileAccess.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class ContractFileService : IContractFileService
    {
        private readonly IContractDocumentGenerator _contractDocumentGenerator;
        private readonly IMinioService _minioService;
        private readonly IFileTypeRepository _fileTypeRepository;
        private readonly IFileAccessUrlService _fileAccessUrlService;
        private readonly IMapper _mapper;

        public ContractFileService(
            IContractDocumentGenerator contractDocumentGenerator,
            IMinioService minioService,
            IFileTypeRepository fileTypeRepository,
            IFileAccessUrlService fileAccessUrlService,
            IMapper mapper)
        {
            _contractDocumentGenerator = contractDocumentGenerator;
            _minioService = minioService;
            _fileTypeRepository = fileTypeRepository;
            _fileAccessUrlService = fileAccessUrlService;
            _mapper = mapper;
        }

        public async Task<File> GenerateAndUploadAsync(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request)
        {
            var document = _contractDocumentGenerator.Generate(BuildContractDocumentData(
                restaurant,
                contractNumber,
                request));

            var fileType = await GetContractDocumentFileTypeAsync();
            var token = await _minioService.UploadFileAsync(new UploadGeneratedFileDto
            {
                Bytes = document.Bytes,
                FileName = document.FileName,
                ContentType = document.ContentType,
                FileTypeId = fileType.Id
            }, BuildUploadPolicy(fileType));

            return _mapper.Map<File>(new FileMapData
            {
                Token = token,
                FileName = document.FileName,
                Size = document.Bytes.LongLength,
                Url = string.Empty,
                FileTypeId = fileType.Id
            });
        }

        public async Task<string?> GenerateFileUrlAsync(File? file)
        {
            if (file is null)
                return null;

            return await _fileAccessUrlService.BuildPrimaryUrlAsync(file);
        }

        private async Task<Domain.Entities.FileType> GetContractDocumentFileTypeAsync()
        {
            var fileType = await _fileTypeRepository.GetByTypeAsync(FileTypeCode.ContractDocument);
            if (fileType is null)
                throw new BusinessRuleException("Contract document file type is not configured.");

            return fileType;
        }

        private static FileUploadPolicy BuildUploadPolicy(Domain.Entities.FileType fileType)
            => new()
            {
                AllowedExtensions = fileType.AllowedExtensions,
                AllowedMimeTypes = fileType.AllowedMimeTypes,
                MaxSizeMb = fileType.MaxSizeMb
            };

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
                Email = restaurant.RestaurantGroup?.Email,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Amount = request.Amount,
                CommissionPercent = request.CommissionPercent,
                StaffSettlementPeriod = request.StaffSettlementPeriod,
                PaymentPolicyId = request.PaymentPolicyId
            };
        }
    }
}
