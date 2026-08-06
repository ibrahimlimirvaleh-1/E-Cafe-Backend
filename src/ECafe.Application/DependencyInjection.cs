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
using ECafe.Application.Services.RestaurantGroup.Abstract;
using ECafe.Application.Services.RestaurantGroup.Concrete;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Application.Services.RestaurantContract.Concrete;
using ECafe.Application.Services.Restaurant.Concrete;
using ECafe.Application.Services.Table.Abstract;
using ECafe.Application.Services.Table.Concrete;
using ECafe.Application.Services.User.Abstract;
using ECafe.Application.Services.User.Concrete;
using ECafe.Application.Services.Workflow.Abstract;
using ECafe.Application.Services.Workflow.Concrete;
using ECafe.Application.Validation;
using ECafe.Infrastructure.Services.MinIO;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ECafe.Application.Services.Notification.Abstract;
using ECafe.Application.Services.Notification.Concrete;
using ECafe.Application.Services.InventoryItem.Abstract;
using ECafe.Application.Services.InventoryItem.Concrete;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.InventoryMovement.Concrete;
using ECafe.Application.Common.Errors;
using ECafe.Domain.Exceptions;
using ECafe.Application.Services.Recipe.Abstract;
using ECafe.Application.Services.Recipe.Concrete;

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

            services.AddSingleton<IErrorMessageProvider, ErrorMessageProvider>();
            services.AddScoped<IMinioService, MinioManager>();
            services.AddScoped<AuditLogManager>();
            services.AddScoped<IAuditLogService>(provider => provider.GetRequiredService<AuditLogManager>());
            services.AddScoped<IAuditOutboxProcessor>(provider => provider.GetRequiredService<AuditLogManager>());
            services.AddScoped<IAuthService, AuthManager>();
            services.AddScoped<IEmailService, EmailManager>();
            services.AddScoped<EmailOutboxManager>();
            services.AddScoped<IEmailOutboxService>(provider => provider.GetRequiredService<EmailOutboxManager>());
            services.AddScoped<IEmailOutboxProcessor>(provider => provider.GetRequiredService<EmailOutboxManager>());
            services.AddScoped<IRestaurantService, RestaurantManager>();
            services.AddScoped<IRestaurantGroupService, RestaurantGroupManager>();
            services.AddScoped<IContractDocumentGenerator, ContractDocumentGenerator>();
            services.AddScoped<IContractFileService, ContractFileService>();
            services.AddScoped<IRestaurantContractService, RestaurantContractManager>();
            services.AddScoped<IUserService, UserManager>();
            services.AddScoped<ITableService, TableManager>();
            services.AddScoped<ICategoryService, CategoryManager>();
            services.AddScoped<IItemService, ItemManager>();
            services.AddScoped<IWorkflowActionService, WorkflowActionManager>();
            services.AddScoped<INotificationService, NotificationManager>();
            services.AddScoped<IInventoryItemService, InventoryItemManager>();
            services.AddScoped<IInventoryMovementService, InventoryMovementManager>();
            services.AddScoped<IRecipeService, RecipeManager>();

            return services;
        }
    }
}
