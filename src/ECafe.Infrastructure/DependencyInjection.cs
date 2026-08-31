using ECafe.Application.Repositories.Category;
using ECafe.Application.Repositories.File;
using ECafe.Application.Repositories.FileType;
using ECafe.Application.Repositories.InventoryItem;
using ECafe.Application.Repositories.InventoryMovement;
using ECafe.Application.Repositories.InventoryMovementType;
using ECafe.Application.Repositories.Item;
using ECafe.Application.Repositories.LoginAttempt;
using ECafe.Application.Repositories.Notification;
using ECafe.Application.Repositories.PasswordResetToken;
using ECafe.Application.Repositories.Recipe;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.RestaurantContract;
using ECafe.Application.Repositories.RestaurantGroup;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.Table;
using ECafe.Application.Repositories.TableSession;
using ECafe.Application.Repositories.Unit;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserPasswordSetupToken;
using ECafe.Application.Repositories.UserRefreshToken;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repository;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Category;
using ECafe.Infrastructure.Repositories.File;
using ECafe.Infrastructure.Repositories.FileType;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.InventoryMovement;
using ECafe.Infrastructure.Repositories.InventoryMovementType;
using ECafe.Infrastructure.Repositories.Item;
using ECafe.Infrastructure.Repositories.LoginAttempt;
using ECafe.Infrastructure.Repositories.Notification;
using ECafe.Infrastructure.Repositories.PasswordResetToken;
using ECafe.Infrastructure.Repositories.Recipe;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.RestaurantContract;
using ECafe.Infrastructure.Repositories.RestaurantGroup;
using ECafe.Infrastructure.Repositories.Role;
using ECafe.Infrastructure.Repositories.Table;
using ECafe.Infrastructure.Repositories.TableSession;
using ECafe.Infrastructure.Repositories.Unit;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserPasswordSetupToken;
using ECafe.Infrastructure.Repositories.UserRefreshToken;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Redis;
using ECafe.Application.Services.Auth.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddScoped<IApplicationDbTransactionFactory, EfApplicationDbTransactionFactory>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            services.AddScoped<IUserPasswordSetupTokenRepository, UserPasswordSetupTokenRepository>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository>();
            services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRestaurantRepository, RestaurantRepository>();
            services.AddScoped<IRestaurantGroupRepository, RestaurantGroupRepository>();
            services.AddScoped<IRestaurantContractRepository, RestaurantContractRepository>();
            services.AddScoped<IUserRestaurantRepository, UserRestaurantRepository>();
            services.AddScoped<ITableRepository, TableRepository>();
            services.AddScoped<ITableSessionRepository, TableSessionRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IItemRepository, ItemRepository>();
            services.AddScoped<IFileRepository, FileRepository>();
            services.AddScoped<IFileTypeRepository, FileTypeRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
            services.AddScoped<IInventoryMovementRepository, InventoryMovementRepository>();
            services.AddScoped<IInventoryMovementTypeRepository, InventoryMovementTypeRepository>();
            services.AddScoped<IRecipeRepository, RecipeRepository>();
            services.AddScoped<IUnitRepository, UnitRepository>();
            services.AddScoped<IUserSessionStateCache, UserSessionStateCache>();
            return services;
        }
    }
}
