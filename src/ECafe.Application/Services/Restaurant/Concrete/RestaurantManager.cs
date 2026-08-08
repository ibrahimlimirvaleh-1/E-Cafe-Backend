using AutoMapper;
using ECafe.Application.Common.Audit;
using ECafe.Application.Common.Outbox;
using ECafe.Application.Common.Pagination;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.User.Staff;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantGroup;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using File = ECafe.Domain.Entities.File;

namespace ECafe.Application.Services.Restaurant.Concrete
{
    public class RestaurantManager : BaseManager, IRestaurantService
    {
        private readonly IRestaurantRepository _restaurantRepository;
        private readonly IRestaurantGroupRepository _restaurantGroupRepository;
        private readonly IUserRestaurantRepository _userRestaurantRepository;
        private readonly IEmailOutboxService _emailOutboxService;
        private readonly IMinioService _minioService;
        private readonly IFileRepository _fileRepository;
        private readonly IAuditLogService _auditLogService;
        private readonly ITableSessionRepository _tableSessionRepository;
        private readonly ILogger<RestaurantManager> _logger;

        public RestaurantManager(
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            IConfiguration configuration,
            IRestaurantRepository restaurantRepository,
            IRestaurantGroupRepository restaurantGroupRepository,
            IEmailOutboxService emailOutboxService,
            IMinioService minioService,
            IUserRestaurantRepository userRestaurantRepository,
            IFileRepository fileRepository,
            IAuditLogService auditLogService,
            ITableSessionRepository tableSessionRepository,
            ILogger<RestaurantManager> logger)
            : base(httpContextAccessor, mapper, configuration)
        {
            _restaurantRepository = restaurantRepository;
            _restaurantGroupRepository = restaurantGroupRepository;
            _emailOutboxService = emailOutboxService;
            _minioService = minioService;
            _userRestaurantRepository = userRestaurantRepository;
            _fileRepository = fileRepository;
            _auditLogService = auditLogService;
            _tableSessionRepository = tableSessionRepository;
            _logger = logger;
        }

        public async Task<PaginatedList<GetAllRestaurantsResponse>> GetAllRestaurantsAsync(
            PaginationFilter filter,
            string? search,
            string? location,
            string? cuisine)
        {
            filter = PaginationFilterNormalizer.Normalize(filter);

            var activeContractStatusId = ((int)ECafe.Domain.Enums.StatusType.Contract * 1000) + (int)ContractStatus.Active;
            var restaurantsQuery = _restaurantRepository.GetRestaurantsForList();

            if (!IsCurrentUserSuperAdmin())
            {
                var currentRestaurantId = GetRequiredCurrentRestaurantId();
                restaurantsQuery = restaurantsQuery.Where(r => r.Id == currentRestaurantId);
            }

            search = NormalizeFilter(search);
            if (search is not null)
            {
                restaurantsQuery = restaurantsQuery.Where(r =>
                    r.Name.ToLower().Contains(search) ||
                    r.Location.ToLower().Contains(search) ||
                    r.Phone.ToLower().Contains(search) ||
                    (r.BranchName != null && r.BranchName.ToLower().Contains(search)) ||
                    (r.RestaurantGroup != null && r.RestaurantGroup.Name.ToLower().Contains(search)));
            }

            location = NormalizeFilter(location);
            if (location is not null)
            {
                restaurantsQuery = restaurantsQuery.Where(r => r.Location.ToLower().Contains(location));
            }

            cuisine = NormalizeFilter(cuisine);
            if (cuisine is not null)
            {
                restaurantsQuery = restaurantsQuery.Where(r =>
                    r.Categories.Any(c =>
                        c.IsActive &&
                        (c.Name.ToLower().Contains(cuisine) ||
                         c.Slug.ToLower().Contains(cuisine) ||
                         c.Items.Any(i =>
                             i.IsActive &&
                             i.IsAvailable &&
                             (i.Name.ToLower().Contains(cuisine) ||
                              (i.Description != null && i.Description.ToLower().Contains(cuisine)))))));
            }

            var totalCount = await restaurantsQuery.CountAsync();
            var restaurants = await restaurantsQuery
                .OrderByDescending(r => r.Contracts.Any(c => c.StatusId == activeContractStatusId))
                .ThenBy(r => r.Name)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var responseTasks = restaurants.Select(MapToGetAllRestaurantResponseAsync);
            var response = (await Task.WhenAll(responseTasks)).ToList();

            return new PaginatedList<GetAllRestaurantsResponse>(
                response,
                totalCount,
                filter.PageNumber,
                filter.PageSize);
        }

        public async Task<GetByIdRestaurantResponse> GetRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var restaurant = await _restaurantRepository.GetRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            var response = Mapper.Map<GetByIdRestaurantResponse>(restaurant);

            await Task.WhenAll(
                PopulateRestaurantImageUrlsAsync(response, restaurant),
                PopulateCategoryItemFileUrlsAsync(response, restaurant));

            return response;
        }

        public async Task<PaginatedList<PublicRestaurantListItemDto>> GetPublicRestaurantsAsync(
            PaginationFilter filter,
            string? search)
        {
            filter = PaginationFilterNormalizer.Normalize(filter);

            var restaurantsQuery = _restaurantRepository.GetActiveRestaurants();

            search = NormalizeFilter(search);
            if (search is not null)
            {
                restaurantsQuery = restaurantsQuery.Where(r =>
                    r.Name.ToLower().Contains(search) ||
                    r.Location.ToLower().Contains(search) ||
                    r.Phone.ToLower().Contains(search) ||
                    (r.BranchName != null && r.BranchName.ToLower().Contains(search)) ||
                    (r.RestaurantGroup != null && r.RestaurantGroup.Name.ToLower().Contains(search)) ||
                    r.Categories.Any(c =>
                        c.IsActive &&
                        (c.Name.ToLower().Contains(search) ||
                         c.Slug.ToLower().Contains(search) ||
                         c.Items.Any(i =>
                             i.IsActive &&
                             i.IsAvailable &&
                             (i.Name.ToLower().Contains(search) ||
                              (i.Description != null && i.Description.ToLower().Contains(search)))))));
            }

            var totalCount = await restaurantsQuery.CountAsync();
            var restaurants = await restaurantsQuery
                .OrderBy(r => r.Name)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var responseTasks = restaurants.Select(MapToPublicListItemAsync);
            var response = (await Task.WhenAll(responseTasks)).ToList();

            return new PaginatedList<PublicRestaurantListItemDto>(
                response,
                totalCount,
                filter.PageNumber,
                filter.PageSize);
        }

        public async Task<PublicRestaurantProfileDto> GetPublicRestaurantProfileAsync(int restaurantId)
        {
            var restaurant = await GetPublicRestaurantEntityAsync(restaurantId);

            return new PublicRestaurantProfileDto
            {
                Restaurant = await MapToPublicDetailAsync(restaurant),
                Tables = MapToPublicTables(restaurant),
                Menu = await MapToPublicMenuAsync(restaurant),
                Staff = await MapToPublicStaffAsync(restaurant)
            };
        }

        public async Task<List<PublicMenuCategoryDto>> GetPublicRestaurantMenuAsync(int restaurantId)
        {
            var restaurant = await GetPublicRestaurantEntityAsync(restaurantId);
            return await MapToPublicMenuAsync(restaurant);
        }

        public async Task<List<PublicStaffDto>> GetPublicRestaurantStaffAsync(int restaurantId)
        {
            var restaurant = await GetPublicRestaurantEntityAsync(restaurantId);
            return await MapToPublicStaffAsync(restaurant);
        }

        public async Task<List<PublicTableDto>> GetPublicRestaurantTablesAsync(int restaurantId)
        {
            var restaurant = await GetPublicRestaurantEntityAsync(restaurantId);
            return MapToPublicTables(restaurant);
        }

        public async Task<List<StaffPublicResponseDto>> GetRestaurantPublicStaffAsync(int restaurantId)
        {
            var staffs = await GetRestaurantStaffEntitiesAsync(restaurantId);
            var activeCounts = await GetActiveTableSessionCountsAsync(restaurantId, staffs);
            var tasks = staffs.Select(staff => MapToPublicDtoAsync(staff, activeCounts.GetValueOrDefault(staff.UserId)));
            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<List<StaffDetailResponseDto>> GetRestaurantStaffAsync(int restaurantId)
        {
            var staffs = await GetRestaurantStaffEntitiesAsync(restaurantId);
            var activeCounts = await GetActiveTableSessionCountsAsync(restaurantId, staffs);
            var tasks = staffs.Select(staff => MapToDetailDtoAsync(staff, activeCounts.GetValueOrDefault(staff.UserId)));
            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<int> RegisterRestaurantAsync(RegisterRestaurantRequest request)
        {
            if (request is null)
                throw new BusinessRuleException("Request is required!");

            await EnsureRestaurantDoesNotExistAsync(request.Name, request.Email, request.Phone);

            var restaurantGroup = await ResolveRestaurantGroupAsync(
                request.RestaurantGroupId,
                request.RestaurantGroupName,
                request.RestaurantGroupLegalName);

            var restaurant = Mapper.Map<Domain.Entities.Restaurant>(request);
            restaurant.Files = [];
            restaurant.BranchName = NormalizeBranchName(request.BranchName, request.Name, restaurantGroup is not null);
            restaurant.RestaurantGroup = restaurantGroup;

            await EnsureBranchDoesNotExistAsync(restaurantGroup?.Id, restaurant.BranchName);
            await AttachRestaurantFilesAsync(restaurant, request.FileIds);

            await _restaurantRepository.Add(restaurant);
            await _restaurantRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurant.Id,
                AuditActions.RestaurantCreated,
                new
                {
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.BranchName,
                    restaurant.Location,
                    restaurant.Phone,
                    restaurant.Email,
                    restaurant.RestaurantGroupId
                },
                AuditEntityTypes.Restaurant,
                restaurant.Id,
                restaurant.Name);

            await _emailOutboxService.EnqueueEmailAsync(
                restaurant.Email,
                restaurant.Name,
                "Restoran qeydiyyatı tamamlandı",
                $"{restaurant.Name} uğurla qeydiyyatdan keçdi.",
                OutboxAggregateTypes.Restaurant,
                restaurant.Id,
                AuditEntityTypes.Restaurant,
                restaurant.Id);

            return restaurant.Id;
        }

        public async Task UpdateRestaurantAsync(int restaurantId, UpdateRestaurantRequest request)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            if (request is null)
                throw new BusinessRuleException("Request is required!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var restaurant = await GetTrackedRestaurantAsync(restaurantId);

            await EnsureRestaurantDoesNotExistAsync(request.Name, request.Email, request.Phone, restaurantId);

            if (request.RestaurantGroupId.GetValueOrDefault() > 0 ||
                !string.IsNullOrWhiteSpace(request.RestaurantGroupName))
            {
                var restaurantGroup = await ResolveRestaurantGroupAsync(
                    request.RestaurantGroupId,
                    request.RestaurantGroupName,
                    request.RestaurantGroupLegalName);

                restaurant.RestaurantGroup = restaurantGroup;
                restaurant.RestaurantGroupId = restaurantGroup is null || restaurantGroup.Id == 0
                    ? null
                    : restaurantGroup.Id;
            }

            var branchName = NormalizeBranchName(
                request.BranchName,
                request.Name,
                restaurant.RestaurantGroup is not null || restaurant.RestaurantGroupId.HasValue);
            var groupIdForDuplicateCheck = restaurant.RestaurantGroupId;
            await EnsureBranchDoesNotExistAsync(groupIdForDuplicateCheck, branchName, restaurantId);

            restaurant.Name = request.Name.Trim();
            restaurant.Location = request.Location.Trim();
            restaurant.Phone = request.Phone.Trim();
            restaurant.Email = request.Email.Trim().ToLowerInvariant();
            restaurant.BranchName = branchName;
            restaurant.DepositAmount = request.DepositAmount;
            restaurant.CancellationWindowMinutes = request.CancellationWindowMinutes;
            restaurant.ServiceFeePercent = request.ServiceFeePercent;
            restaurant.StaffSettlementPeriod = request.StaffSettlementPeriod;
            restaurant.DefaultWaiterTableLimit = request.DefaultWaiterTableLimit;

            if (request.FileIds is not null)
                await ReplaceRestaurantFilesAsync(restaurant, request.FileIds);

            await _restaurantRepository.Update(restaurant);
            await _restaurantRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurant.Id,
                AuditActions.RestaurantUpdated,
                new
                {
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.BranchName,
                    restaurant.Location,
                    restaurant.Phone,
                    restaurant.Email,
                    restaurant.DepositAmount,
                    restaurant.CancellationWindowMinutes,
                    restaurant.ServiceFeePercent,
                    restaurant.StaffSettlementPeriod,
                    restaurant.DefaultWaiterTableLimit,
                    restaurant.RestaurantGroupId
                },
                AuditEntityTypes.Restaurant,
                restaurant.Id,
                restaurant.Name);
        }

        public async Task DeactivateRestaurantAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            var restaurant = await GetTrackedRestaurantAsync(restaurantId);

            restaurant.IsActive = false;

            foreach (var userRestaurant in restaurant.UserRestaurants.Where(x => x.IsActive))
                userRestaurant.IsActive = false;

            await _restaurantRepository.Update(restaurant);
            await _restaurantRepository.SaveChangesAsync();

            await _auditLogService.RecordRestaurantActionAsync(
                restaurant.Id,
                AuditActions.RestaurantDeactivated,
                new
                {
                    restaurant.Id,
                    restaurant.Name
                },
                AuditEntityTypes.Restaurant,
                restaurant.Id,
                restaurant.Name);
        }

        private async Task<GetAllRestaurantsResponse> MapToGetAllRestaurantResponseAsync(Domain.Entities.Restaurant restaurant)
        {
            var response = Mapper.Map<GetAllRestaurantsResponse>(restaurant);

            response.ImageUrls = restaurant.Files is null
                ? []
                : await GenerateFileUrlsAsync(restaurant.Files);

            return response;
        }

        private static string? NormalizeFilter(string? value)
        {
            var normalized = value?.Trim().ToLower();
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private async Task AttachRestaurantFilesAsync(
            Domain.Entities.Restaurant restaurant,
            List<int>? fileIds)
        {
            if (fileIds is null || fileIds.Count == 0)
                return;

            var files = await GetAttachableFilesAsync(fileIds);
            restaurant.Files ??= [];
            restaurant.Files.AddRange(files);
        }

        private async Task ReplaceRestaurantFilesAsync(
            Domain.Entities.Restaurant restaurant,
            List<int> fileIds)
        {
            restaurant.Files ??= [];

            if (fileIds.Count == 0)
            {
                restaurant.Files.Clear();
                return;
            }

            var uniqueFileIds = fileIds.Distinct().ToList();
            var currentFiles = restaurant.Files
                .Where(file => uniqueFileIds.Contains(file.Id))
                .ToList();
            var missingIds = uniqueFileIds
                .Except(currentFiles.Select(file => file.Id))
                .ToList();
            var attachableFiles = await GetAttachableFilesAsync(missingIds);

            restaurant.Files.Clear();
            restaurant.Files.AddRange(currentFiles.Concat(attachableFiles));
        }

        private async Task<List<File>> GetAttachableFilesAsync(IEnumerable<int> fileIds)
        {
            var uniqueFileIds = fileIds.Distinct().ToList();
            if (uniqueFileIds.Any(fileId => fileId <= 0))
                throw new BusinessRuleException("Invalid file ID!");

            var files = await _fileRepository.GetAttachableByIdsAsync(uniqueFileIds);
            var foundIds = files.Select(file => file.Id).ToHashSet();
            var missingIds = uniqueFileIds.Where(fileId => !foundIds.Contains(fileId)).ToList();
            if (missingIds.Count > 0)
                throw new BusinessRuleException($"File(s) not found or already attached: {string.Join(", ", missingIds)}");

            return files;
        }

        private async Task PopulateRestaurantImageUrlsAsync(
            GetByIdRestaurantResponse response,
            Domain.Entities.Restaurant restaurant)
        {
            response.Restaurant.ImageUrls = restaurant.Files is null
                ? []
                : await GenerateFileUrlsAsync(restaurant.Files);
        }

        private async Task PopulateCategoryItemFileUrlsAsync(
            GetByIdRestaurantResponse response,
            Domain.Entities.Restaurant restaurant)
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
            assign(await TryGenerateFileUrlAsync(token));
        }

        private async Task<Domain.Entities.Restaurant> GetPublicRestaurantEntityAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            var restaurant = await _restaurantRepository.GetPublicRestaurantInfoAsync(restaurantId);
            if (restaurant is null)
                throw new BusinessRuleException("Public restaurant not found!");

            return restaurant;
        }

        private async Task<PublicRestaurantListItemDto> MapToPublicListItemAsync(Domain.Entities.Restaurant restaurant)
        {
            var response = Mapper.Map<PublicRestaurantListItemDto>(restaurant);
            response.ImageUrls = await GenerateRestaurantImageUrlsAsync(restaurant);

            return response;
        }

        private async Task<PublicRestaurantDetailDto> MapToPublicDetailAsync(Domain.Entities.Restaurant restaurant)
        {
            var response = Mapper.Map<PublicRestaurantDetailDto>(restaurant);
            response.ImageUrls = await GenerateRestaurantImageUrlsAsync(restaurant);

            return response;
        }

        private async Task<List<string>> GenerateRestaurantImageUrlsAsync(Domain.Entities.Restaurant restaurant)
        {
            if (restaurant.Files is null || restaurant.Files.Count == 0)
                return [];

            return await GenerateFileUrlsAsync(restaurant.Files);
        }

        private async Task<List<PublicMenuCategoryDto>> MapToPublicMenuAsync(Domain.Entities.Restaurant restaurant)
        {
            var categories = restaurant.Categories
                .Where(category => category.IsActive)
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .ToList();

            var response = Mapper.Map<List<PublicMenuCategoryDto>>(categories);
            var itemLookup = categories
                .SelectMany(category => category.Items)
                .ToDictionary(item => item.Id);

            foreach (var item in response.SelectMany(category => category.Items))
            {
                var token = itemLookup.TryGetValue(item.Id, out var entity)
                    ? entity.File?.Token
                    : null;

                await AssignFileUrlAsync(token, url => item.FileUrl = url);
            }

            return response;
        }

        private async Task<List<PublicStaffDto>> MapToPublicStaffAsync(Domain.Entities.Restaurant restaurant)
        {
            var staffs = restaurant.UserRestaurants
                .Where(staff =>
                    staff.IsActive &&
                    staff.User.IsActive &&
                    staff.User.RoleId == (int)RoleCode.Waiter)
                .OrderBy(staff => staff.User.Name)
                .ThenBy(staff => staff.User.Surname)
                .ToList();

            var activeCounts = await GetActiveTableSessionCountsAsync(restaurant.Id, staffs);
            var staffTasks = staffs.Select(async staff =>
            {
                var response = Mapper.Map<PublicStaffDto>(staff);
                response.FileUrl = await GenerateFileUrlAsync(staff.User.File);
                ApplyTableCapacity(response, staff, activeCounts.GetValueOrDefault(staff.UserId));

                return response;
            });

            return (await Task.WhenAll(staffTasks)).ToList();
        }

        private List<PublicTableDto> MapToPublicTables(Domain.Entities.Restaurant restaurant)
        {
            var tables = restaurant.Tables
                .Where(table => table.IsActive)
                .OrderBy(table => table.TableNo)
                .ToList();

            return Mapper.Map<List<PublicTableDto>>(tables);
        }

        private async Task<List<UserRestaurant>> GetRestaurantStaffEntitiesAsync(int restaurantId)
        {
            if (restaurantId <= 0)
                throw new BusinessRuleException("Invalid restaurant ID!");

            EnsureCurrentUserCanAccessRestaurant(restaurantId);

            return await _userRestaurantRepository
                .GetRestaurantStaffAsync(restaurantId);
        }

        private async Task<Domain.Entities.Restaurant> GetTrackedRestaurantAsync(int restaurantId)
        {
            var restaurant = await _restaurantRepository.QueryTracked(x => x.Id == restaurantId)
                .Include(x => x.RestaurantGroup)
                .Include(x => x.Files)
                .Include(x => x.UserRestaurants)
                    .ThenInclude(x => x.User)
                .FirstOrDefaultAsync();

            if (restaurant is null)
                throw new BusinessRuleException("Restaurant not found!");

            return restaurant;
        }

        private async Task EnsureRestaurantDoesNotExistAsync(
            string name,
            string email,
            string phone,
            int? excludedRestaurantId = null)
        {
            var normalizedName = name.Trim();
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var normalizedPhone = phone.Trim();

            var query = _restaurantRepository.Query();

            if (excludedRestaurantId.HasValue)
                query = query.Where(x => x.Id != excludedRestaurantId.Value);

            if (await query.AnyAsync(x => x.Email == normalizedEmail))
                throw new BusinessRuleException("Restaurant with this email already exists!");

            if (await query.AnyAsync(x => x.Name == normalizedName))
                throw new BusinessRuleException("Restaurant with this name already exists!");

            if (await query.AnyAsync(x => x.Phone == normalizedPhone))
                throw new BusinessRuleException("Restaurant with this phone already exists!");
        }

        private async Task<Domain.Entities.RestaurantGroup?> ResolveRestaurantGroupAsync(
            int? restaurantGroupId,
            string? restaurantGroupName,
            string? restaurantGroupLegalName)
        {
            var groupId = restaurantGroupId.GetValueOrDefault();

            if (groupId < 0)
                throw new BusinessRuleException("Invalid restaurant group ID!");

            if (groupId > 0)
            {
                var existingGroup = await _restaurantGroupRepository
                    .QueryTracked(x => x.Id == groupId)
                    .FirstOrDefaultAsync();

                if (existingGroup is null)
                    throw new BusinessRuleException("Restaurant group not found!");

                if (!existingGroup.IsActive)
                    throw new BusinessRuleException("Restaurant group is inactive!");

                return existingGroup;
            }

            if (string.IsNullOrWhiteSpace(restaurantGroupName))
                return null;

            var groupName = restaurantGroupName.Trim();

            var groupNameExists = await _restaurantGroupRepository
                .CheckExistAsync(x => x.Name == groupName);

            if (groupNameExists)
                throw new BusinessRuleException("Restaurant group with this name already exists!");

            return new Domain.Entities.RestaurantGroup
            {
                Name = groupName,
                LegalName = string.IsNullOrWhiteSpace(restaurantGroupLegalName)
                    ? null
                    : restaurantGroupLegalName.Trim(),
                IsActive = true
            };
        }

        private async Task EnsureBranchDoesNotExistAsync(
            int? restaurantGroupId,
            string? branchName,
            int? excludedRestaurantId = null)
        {
            if (!restaurantGroupId.HasValue || string.IsNullOrWhiteSpace(branchName))
                return;

            var normalizedBranchName = branchName.Trim();
            var query = _restaurantRepository.Query(x =>
                x.RestaurantGroupId == restaurantGroupId.Value &&
                x.BranchName == normalizedBranchName);

            if (excludedRestaurantId.HasValue)
                query = query.Where(x => x.Id != excludedRestaurantId.Value);

            if (await query.AnyAsync())
                throw new BusinessRuleException("Branch with this name already exists in the selected restaurant group!");
        }

        private static string? NormalizeBranchName(string? branchName, string fallbackName, bool hasRestaurantGroup)
        {
            if (!string.IsNullOrWhiteSpace(branchName))
                return branchName.Trim();

            return hasRestaurantGroup ? fallbackName.Trim() : null;
        }

        private async Task<StaffPublicResponseDto> MapToPublicDtoAsync(UserRestaurant staff, int activeTableSessionCount)
        {
            var response = Mapper.Map<StaffPublicResponseDto>(staff);
            response.FileUrl = await GenerateFileUrlAsync(staff.User.File);
            ApplyTableCapacity(response, staff, activeTableSessionCount);
            return response;
        }

        private async Task<StaffDetailResponseDto> MapToDetailDtoAsync(UserRestaurant staff, int activeTableSessionCount)
        {
            var response = Mapper.Map<StaffDetailResponseDto>(staff);
            response.FileUrl = await GenerateFileUrlAsync(staff.User.File);
            ApplyTableCapacity(response, staff, activeTableSessionCount);
            return response;
        }

        private async Task<Dictionary<int, int>> GetActiveTableSessionCountsAsync(
            int restaurantId,
            IReadOnlyCollection<UserRestaurant> staffs)
        {
            var waiterUserIds = staffs
                .Where(staff => staff.User.RoleId == (int)RoleCode.Waiter)
                .Select(staff => staff.UserId)
                .Distinct()
                .ToList();

            return await _tableSessionRepository.GetOpenSessionCountsByWaitersAsync(restaurantId, waiterUserIds);
        }

        private static void ApplyTableCapacity(PublicStaffDto response, UserRestaurant staff, int activeTableSessionCount)
        {
            if (staff.User.RoleId != (int)RoleCode.Waiter)
            {
                response.ActiveTableSessionCount = 0;
                response.EffectiveMaxActiveTableCount = null;
                response.CanAcceptMoreTables = false;
                return;
            }

            var effectiveLimit = staff.MaxActiveTableCount ?? staff.Restaurant.DefaultWaiterTableLimit;

            response.ActiveTableSessionCount = activeTableSessionCount;
            response.EffectiveMaxActiveTableCount = effectiveLimit;
            response.CanAcceptMoreTables = !effectiveLimit.HasValue || activeTableSessionCount < effectiveLimit.Value;
        }

        private static void ApplyTableCapacity(StaffBaseDto response, UserRestaurant staff, int activeTableSessionCount)
        {
            if (staff.User.RoleId != (int)RoleCode.Waiter)
            {
                response.ActiveTableSessionCount = 0;
                response.EffectiveMaxActiveTableCount = null;
                response.CanAcceptMoreTables = false;
                return;
            }

            var effectiveLimit = staff.MaxActiveTableCount ?? staff.Restaurant.DefaultWaiterTableLimit;

            response.ActiveTableSessionCount = activeTableSessionCount;
            response.EffectiveMaxActiveTableCount = effectiveLimit;
            response.CanAcceptMoreTables = !effectiveLimit.HasValue || activeTableSessionCount < effectiveLimit.Value;
        }

        private async Task<string?> GenerateFileUrlAsync(File? file)
        {
            if (file == null)
                return null;

            return await TryGenerateFileUrlAsync(file.Token);
        }

        private async Task<List<string>> GenerateFileUrlsAsync(IEnumerable<File> files)
        {
            var urls = await Task.WhenAll(files.Select(file => TryGenerateFileUrlAsync(file.Token)));

            return urls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!)
                .ToList();
        }

        private async Task<string?> TryGenerateFileUrlAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            try
            {
                return await _minioService.GenerateFileUrl(token);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to generate file URL for token {FileToken}.",
                    token);

                return null;
            }
        }
    }
}
