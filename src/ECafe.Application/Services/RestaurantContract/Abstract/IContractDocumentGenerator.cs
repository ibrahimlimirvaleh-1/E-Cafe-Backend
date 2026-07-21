using ECafe.Application.DTOs.RestaurantContract;

namespace ECafe.Application.Services.RestaurantContract.Abstract
{
    public interface IContractDocumentGenerator
    {
        GeneratedContractDocument Generate(RestaurantContractDocumentData data);
    }
}
