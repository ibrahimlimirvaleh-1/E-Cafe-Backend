using ECafe.Application.DTOs.RestaurantContract;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.RestaurantContract.Abstract
{
    public interface IContractFileService
    {
        Task<File> GenerateAndUploadAsync(
            Domain.Entities.Restaurant restaurant,
            string contractNumber,
            CreateRestaurantContractRequest request);

        Task<string?> GenerateFileUrlAsync(File? file);
    }
}
