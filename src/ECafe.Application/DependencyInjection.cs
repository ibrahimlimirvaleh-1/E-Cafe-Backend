using ECafe.Application.Mappings;
using ECafe.Application.Services;
using ECafe.Application.Services.AuditLog.Abstract;
using ECafe.Application.Services.AuditLog.Concrete;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.Category.Abstract;
using ECafe.Application.Services.Item.Abstract;
using ECafe.Application.Services.Item.Concrete;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Restaurant.Abstract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.RestaurantContract.Concrete;
using ECafe.Application.Services.Restaurant.Concrete;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Application.Services.Table.Concrete;
using ECafe.Application.Services.User.Abstract;
using ECafe.Application.Services.User.Concrete;
using ECafe.Application.Validation;
using ECafe.Infrastructure.Services.MinIO;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ECafe.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {

            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            services.AddAutoMapper(_ => { }, typeof(DependencyInjection).Assembly);


            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IMinioService, MinioManager>();
            services.AddScoped<AuditLogManager>();
            services.AddScoped<IAuditLogService>(provider => provider.GetRequiredService<AuditLogManager>());
            services.AddScoped<IAuditOutboxProcessor>(provider => provider.GetRequiredService<AuditLogManager>());
            services.AddScoped<IAuthService, AuthManager>();
            services.AddScoped<IEmailService, EmailManager>();
            services.AddScoped<IRestaurantService, RestaurantManager>();
            services.AddScoped<IContractDocumentGenerator, ContractDocumentGenerator>();
            services.AddScoped<IRestaurantContractService, RestaurantContractManager>();
            services.AddScoped<IUserService, UserManager>();
            services.AddScoped<ITableService, TableManager>();
            services.AddScoped<ICategoryService, CategoryManager>();
            services.AddScoped<IItemService, ItemManager>();

            return services;
        }
    }
}
