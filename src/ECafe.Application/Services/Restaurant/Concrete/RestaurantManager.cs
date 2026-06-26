using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio.DataModel;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.Restaurant.Concrete
{
    public class RestaurantManager : BaseManager, IRestaurantService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IUserRestaurantRepository _userRestaurantRepository;
        private readonly IEmailService _emailService;
        private readonly IMinioService _minioService;
        public RestaurantManager(IHttpContextAccessor httpContextAccessor,
                                 IMapper mapper, IConfiguration configuration,
                                 IRestaurantRepository restaurantRepository,
                                 IEmailService emailService,
                                 IMinioService minioService, IUserRestaurantRepository userRestaurantRepository)
                                 : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _emailService = emailService;
            _minioService = minioService;
            _userRestaurantRepository = userRestaurantRepository;
        }

        public async Task<List<GetAllRestaurantsResponse>> GetAllRestaurantsAsync()
        {
            var restaurants = await _restaurantRepository.GetActiveRestaurants()
                .ToListAsync();

            var responseTasks = restaurants.Select(async restaurant => new GetAllRestaurantsResponse
            {
                Id = restaurant.Id,
                Name = restaurant.Name,
                Location = restaurant.Location,
                Phone = restaurant.Phone,
                RatingAverage = restaurant.RatingAverage,
                RatingCount = restaurant.RatingCount,
                ImageUrls = restaurant.Files is null
                    ? []
                    : (await Task.WhenAll(restaurant.Files.Select(file => _minioService.GenerateFileUrl(file.Token)))).ToList()
            });

            return (await Task.WhenAll(responseTasks)).ToList();
        }

        public async Task<GetByIdRestaurantResponse> GetRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            var response = Mapper.Map<GetByIdRestaurantResponse>(restaurant);

            await Task.WhenAll(
                    PopulateRestaurantImageUrlsAsync(response, restaurant),
                    PopulateUserFileUrlsAsync(response, restaurant),
                    PopulateCategoryItemFileUrlsAsync(response, restaurant) // ✅
                    );

            return response;
        }

        private async Task PopulateRestaurantImageUrlsAsync(
            GetByIdRestaurantResponse response,
            Domain.Entities.Restaurant restaurant)
        {
            response.Restaurant.ImageUrls = restaurant.Files is null
                ? []
                : (await Task.WhenAll(
                    restaurant.Files.Select(f => _minioService.GenerateFileUrl(f.Token))
                  )).ToList();
        }

        private async Task PopulateUserFileUrlsAsync(GetByIdRestaurantResponse response,Domain.Entities.Restaurant restaurant)
        {
            var userLookup = restaurant.UserRestaurants
                .ToDictionary(x => x.User.Id, x => x.User); 

            await Task.WhenAll(response.Users.Select(userDto =>
            {
                var token = userLookup.TryGetValue(userDto.Id, out var user)
                    ? user.File?.Token
                    : null;

                return AssignFileUrlAsync(token, url => userDto.FileUrl = url);
            }));
        }

        private async Task PopulateCategoryItemFileUrlsAsync( GetByIdRestaurantResponse response, Domain.Entities.Restaurant restaurant)
        {
            var itemLookup = restaurant.Categories
                .SelectMany(c => c.Items)
                .ToDictionary(x => x.Id);

            var allItemDtos = response.Categories.SelectMany(c => c.Items);

            await Task.WhenAll(allItemDtos.Select(itemDto =>
            {
                var token = itemLookup.TryGetValue(itemDto.Id, out var entity)
                    ? entity.File?.Token
                    : null;

                return AssignFileUrlAsync(token, url => itemDto.FileUrl = url);
            }));
        }

        private async Task AssignFileUrlAsync(string? token, Action<string?> assign)
        {
            assign(token is not null
                ? await _minioService.GenerateFileUrl(token)
                : null);
        }

        public async Task<List<StaffPublicResponseDto>> GetRestaurantPublicStaffAsync(int restaurantId)
        {
            var staffs = await GetRestaurantStaffEntitiesAsync(restaurantId);

            var tasks = staffs.Select(MapToPublicDtoAsync);

            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<List<StaffDetailResponseDto>> GetRestaurantStaffAsync(int restaurantId)
        {
            var staffs = await GetRestaurantStaffEntitiesAsync(restaurantId);

            var tasks = staffs.Select(MapToDetailDtoAsync);

            return (await Task.WhenAll(tasks)).ToList();
        }



        public async Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("request is not null!");

            var restaurant = new Domain.Entities.Restaurant
            {
                Name = request.Name,
                Location = request.Location,
                Phone = request.Phone,
                Email = request.Email,
                RatingAverage = 0,
                RatingCount = 0,
                IsActive = true,
                Files = new List<Domain.Entities.File>()
            };

            if (request.Files is not null && request.Files.Any())
            {
                foreach (var formFile in request.Files)
                {
                    if (formFile is null || formFile.Length == 0)
                        continue;

                    var token = await _minioService.UploadFileAsync(new UploadFileDto(formFile));

                    var fileEntity = new Domain.Entities.File
                    {
                        Token = token,
                        Name = Path.GetFileNameWithoutExtension(formFile.FileName),
                        Extension = Path.GetExtension(formFile.FileName),
                        Size = formFile.Length,
                        Url = await _minioService.GenerateFileUrl(token)
                    };

                    restaurant.Files.Add(fileEntity);
                }
            }


            await _restaurantRepository.Add(restaurant);
            await _restaurantRepository.SaveChangesAsync();

            await _emailService.SendMailAsync(restaurant.Email, restaurant.Name);

            return restaurant.Id;
        }



        #region Helpers
        private async Task<List<UserRestaurant>> GetRestaurantStaffEntitiesAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            return await _userRestaurantRepository
                .GetRestaurantStaffAsync(restaurantId);
        }

        private async Task<StaffPublicResponseDto> MapToPublicDtoAsync(UserRestaurant staff)
        {
            return new StaffPublicResponseDto
            {
                Id = staff.User.Id,
                Name = staff.User.Name,
                Surname = staff.User.Surname,
                FileUrl = await GenerateFileUrlAsync(staff.User.File),
                Role = staff.User.Role.Name
            };
        }

        private async Task<StaffDetailResponseDto> MapToDetailDtoAsync(UserRestaurant staff)
        {
            return new StaffDetailResponseDto
            {
                Id = staff.User.Id,
                Name = staff.User.Name,
                Surname = staff.User.Surname,
                FileUrl = await GenerateFileUrlAsync(staff.User.File),
                Email = staff.User.Email,
                Phone = staff.User.Phone,
                Role = staff.User.Role.Name
            };
        }

        private async Task<string?> GenerateFileUrlAsync(File? file)
        {
            if (file == null)
                return null;

            return await _minioService.GenerateFileUrl(file.Token);
        }
        #endregion
    }
}
