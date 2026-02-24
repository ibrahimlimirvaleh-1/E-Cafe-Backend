using ECafe.Application.Services.MinIO.Abstracts;
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

            return services;
        }
    }
}
