using AutoMapper;
using ECafe.Application.DTOs.Auth;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.RestaurantContract.Abstract;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class ContractFileService : IContractFileService
    {
        private readonly IContractDocumentGenerator _contractDocumentGenerator;
        private readonly IMinioService _minioService;
        private readonly IMapper _mapper;

        public ContractFileService(
            IContractDocumentGenerator contractDocumentGenerator,
            IMinioService minioService,
            IMapper mapper)
        {
            _contractDocumentGenerator = contractDocumentGenerator;
            _minioService = minioService;
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

            var token = await _minioService.UploadFileAsync(new UploadGeneratedFileDto
            {
                Bytes = document.Bytes,
                FileName = document.FileName,
                ContentType = document.ContentType
            });

            return _mapper.Map<File>(new FileMapData
            {
                Token = token,
                FileName = document.FileName,
                Size = document.Bytes.LongLength,
                Url = await _minioService.GenerateFileUrl(token)
            });
        }

        public async Task<string?> GenerateFileUrlAsync(File? file)
        {
            if (file is null)
                return null;

            return await _minioService.GenerateFileUrl(file.Token);
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
