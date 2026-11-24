using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Common.Configuration;

public static class ConfigurationValueReader
{
    /// <summary>Müsbət tam konfiqurasiya dəyərini oxuyur; boş, səhv, sıfır və mənfi dəyərdə çağıranın əvvəlki fallback-ini saxlayır.</summary>
    public static int GetPositiveInt(IConfiguration configuration, string key, int fallback)
        => int.TryParse(configuration[key], out var value) && value > 0 ? value : fallback;
}
