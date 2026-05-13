using ECafe.Application.Repositories.Table;
using ECafe.Application.Services;
using ECafe.Application.Services.Auth.Abstract;
using ECafe.Application.Services.Auth.Concrete;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Application.Services.Restaurant.Abstract;
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

            services.AddAutoMapper(typeof(DependencyInjection).Assembly);


            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IMinioService, MinioManager>();
            services.AddScoped<IAuthService, AuthManager>();
            services.AddScoped<IEmailService, EmailManager>();
            services.AddScoped<IRestaurantService, RestaurantManager>();
            services.AddScoped<IUserService, UserManager>();
            services.AddScoped<ITableService,TableManager>();

            return services;
        }
    }
}
