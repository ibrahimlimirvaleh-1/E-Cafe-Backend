using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantGroup;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Category;
using ECafe.Infrastructure.Repositories.File;
using ECafe.Infrastructure.Repositories.Item;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.RestaurantGroup;
using ECafe.Infrastructure.Repositories.RestaurantContract;
using ECafe.Infrastructure.Repositories.Role;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ECafe.Application.Repositories.Notification;
using ECafe.Infrastructure.Repositories.Notification;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.InventoryItem;
namespace ECafe.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            var connStr = configuration.GetConnectionString("ECafeDb");
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("Connection string 'ECafeDb' is missing.");

            services.AddScoped<PermissionCacheInvalidationInterceptor>();

            services.AddDbContext<ECafeDbContext>((serviceProvider, options) =>
                options
                    .UseNpgsql(connStr)
                    .AddInterceptors(serviceProvider.GetRequiredService<PermissionCacheInvalidationInterceptor>()));

            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRestaurantRepository, RestaurantRepository>();
            services.AddScoped<IRestaurantGroupRepository, RestaurantGroupRepository>();
            services.AddScoped<IRestaurantContractRepository, RestaurantContractRepository>();
            services.AddScoped<IUserRestaurantRepository, UserRestaurantRepository>();
            services.AddScoped<ITableRepository, TableRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IItemRepository, ItemRepository>();
            services.AddScoped<IFileRepository, FileRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
            return services;
        }
    }
}

