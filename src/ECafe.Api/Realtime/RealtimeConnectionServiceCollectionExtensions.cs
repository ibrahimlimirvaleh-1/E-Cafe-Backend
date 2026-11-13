using ECafe.Api.Realtime.Connections;
using ECafe.Api.Realtime.Connections.Abstract;
using ECafe.Api.Realtime.Connections.Concrete;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECafe.Api.Realtime;

public static class RealtimeConnectionServiceCollectionExtensions
{
    /// <summary>Limitləri başlanğıcda yoxlayır; backplane açıqdırsa ortaq Redis, əks halda tək nüsxə üçün yaddaş rezervlərini seçir.</summary>
    public static IServiceCollection AddRealtimeConnectionLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RealtimeConnectionOptions>().Bind(configuration.GetSection(RealtimeConnectionOptions.SectionName))
            .Validate(value => value.MaxConnectionsPerUser is >= 1 and <= 50, "Realtime connection limit must be between 1 and 50.")
            .Validate(value => value.LeaseSeconds is >= 30 and <= 300, "Realtime lease must be between 30 and 300 seconds.")
            .Validate(value => value.RenewalSeconds >= 5 && value.RenewalSeconds <= value.LeaseSeconds / 3,
                "Realtime renewal must be at least 5 seconds and no more than one third of its lease.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        if (configuration.GetValue("SignalR:UseRedisBackplane", false))
            services.AddSingleton<IRealtimeConnectionLeaseStore, RedisRealtimeConnectionLeaseStore>();
        else
            services.AddSingleton<IRealtimeConnectionLeaseStore, MemoryRealtimeConnectionLeaseStore>();
        services.AddSingleton<RealtimeConnectionLimitFilter>();
        return services;
    }
}
