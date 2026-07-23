using AutoMapper;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.Repositories.RestaurantGroup;
using ECafe.Application.Services.RestaurantGroup.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.RestaurantGroup.Concrete
{
    public class RestaurantGroupManager : BaseManager, IRestaurantGroupService
    {
        private readonly IRestaurantGroupRepository _restaurantGroupRepository;

        public RestaurantGroupManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantGroupRepository restaurantGroupRepository)
            : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantGroupRepository = restaurantGroupRepository;
        }

        public async Task<List<RestaurantGroupResponse>> GetAllAsync()
        {
            var groups = await _restaurantGroupRepository
                .Query(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new RestaurantGroupResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    LegalName = x.LegalName,
                    IsActive = x.IsActive
                })
                .ToListAsync();

            return groups;
        }

        public async Task<int> CreateAsync(CreateRestaurantGroupRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required.");

            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new BusinessRuleException("Restaurant group name is required.");

            var exists = await _restaurantGroupRepository
                .CheckExistAsync(x => x.Name == name);

            if (exists)
                throw new BusinessRuleException("Restaurant group with this name already exists!");

            var group = new Domain.Entities.RestaurantGroup
            {
                Name = name,
                LegalName = string.IsNullOrWhiteSpace(request.LegalName)
                    ? null
                    : request.LegalName.Trim(),
                IsActive = true
            };

            await _restaurantGroupRepository.Add(group);
            await _restaurantGroupRepository.SaveChangesAsync();

            return group.Id;
        }
    }
}
