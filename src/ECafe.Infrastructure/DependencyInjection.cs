using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repositories.Role;
using ECafe.Application.Repositories.User;
using ECafe.Application.Repositories.UserRestaurant;
using ECafe.Application.Repositories.UserRole;
using ECafe.Application.Repository;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using ECafe.Infrastructure.Repositories.Restaurant;
using ECafe.Infrastructure.Repositories.Role;
using ECafe.Infrastructure.Repositories.User;
using ECafe.Infrastructure.Repositories.UserRestaurant;
using ECafe.Infrastructure.Repositories.UserRole;
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

            services.AddDbContext<ECafeDbContext>(options =>
                options.UseNpgsql(connStr));

            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IRestaurantRepository, RestaurantRepository>();
            services.AddScoped<IUserRoleRepository, UserRoleRepository>();
            services.AddScoped<IUserRestaurantRepository, UserRestaurantRepository>();
            return services;
        }
    }
}
