using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECafe.Application.DTOs.Geocoding;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.Services.Geocoding.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Geocoding.Concrete;

public sealed class NominatimGeocodingService : IGeocodingService
{
    private const string SupportedProvider = "Nominatim";
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 30;
    private const int MinimumCacheMinutes = 1;
    private const int MaximumCacheMinutes = 10080;
    private const int MinimumResultLimit = 1;
    private const int MaximumResultLimit = 10;

    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _cache;
    private readonly GeocodingOptions _options;

    public NominatimGeocodingService(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        _options = ReadOptions(configuration);
    }

    public async Task<IReadOnlyList<GeocodeAddressResponse>> SearchAddressesAsync(string address, int limit, CancellationToken cancellationToken = default)
    {
        var normalizedAddress = address.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAddress))
            throw new BadRequestException(ErrorCode.GeocodingAddressRequired);

        var normalizedLimit = Math.Clamp(limit, MinimumResultLimit, MaximumResultLimit);
        var cacheKey = $"geocoding:{ResolveCacheProviderKey()}:{normalizedAddress.ToLowerInvariant()}:{normalizedLimit}";
        if (_cache.TryGetValue<IReadOnlyList<GeocodeAddressResponse>>(cacheKey, out var cached) && cached is not null)
            return cached;

        ValidateProviderConfiguration();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ResolveTimeoutSeconds()));

        try
        {
            var matches = await SearchNominatimAsync(normalizedAddress, normalizedLimit, timeoutCts.Token);

            _cache.Set(cacheKey, matches.Take(normalizedLimit).ToList(), TimeSpan.FromMinutes(ResolveCacheMinutes()));
            return matches;
        }
        catch (BaseException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderTimedOut);
        }
        catch (HttpRequestException)
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);
        }
        catch (JsonException)
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingResponseInvalid);
        }
    }

    private async Task<IReadOnlyList<GeocodeAddressResponse>> SearchNominatimAsync(string address, int limit, CancellationToken cancellationToken)
    {
        var matches = new List<GeocodeAddressResponse>();
        foreach (var searchAddress in BuildAddressSearchVariants(address))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildNominatimSearchUri(searchAddress, limit));
            request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent!.Trim());

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(body, JsonOptions) ?? [];
            matches = results
                .Select(MapNominatimResult)
                .Where(result => result is not null)
                .Select(result => result!)
                .Take(limit)
                .ToList();

            if (matches.Count > 0)
                return matches;
        }

        throw new NotFoundException(ErrorCode.GeocodingAddressNotFound);
    }

    private Uri BuildNominatimSearchUri(string address, int limit)
    {
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        var query = new Dictionary<string, string?>
        {
            ["format"] = "jsonv2",
            ["limit"] = limit.ToString(),
            ["addressdetails"] = "1",
            ["q"] = address
        };

        if (!string.IsNullOrWhiteSpace(_options.CountryCodes))
            query["countrycodes"] = _options.CountryCodes.Trim();

        var queryString = string.Join("&", query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));

        return new Uri($"{baseUrl}/search?{queryString}");
    }

    private static GeocodeAddressResponse? MapNominatimResult(NominatimResult result)
    {
        if (string.IsNullOrWhiteSpace(result.DisplayName)
            || !double.TryParse(result.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(result.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }

        return new GeocodeAddressResponse
        {
            DisplayName = result.DisplayName,
            Latitude = latitude,
            Longitude = longitude,
            PlaceId = result.PlaceId?.ToString()
        };
    }

    private static IReadOnlyList<string> BuildAddressSearchVariants(string address)
    {
        var normalizedAddress = address.Trim();
        var variants = new List<string> { normalizedAddress };
        var hasBakuContext = normalizedAddress.Contains("baku", StringComparison.OrdinalIgnoreCase)
            || normalizedAddress.Contains("baki", StringComparison.OrdinalIgnoreCase)
            || normalizedAddress.Contains("bakı", StringComparison.OrdinalIgnoreCase);
        var hasAzerbaijanContext = normalizedAddress.Contains("azerbaijan", StringComparison.OrdinalIgnoreCase)
            || normalizedAddress.Contains("azərbaycan", StringComparison.OrdinalIgnoreCase);

        if (!hasBakuContext)
            variants.Add($"{normalizedAddress} Baku");

        if (!hasAzerbaijanContext)
            variants.Add($"{normalizedAddress} Azerbaijan");

        if (!hasBakuContext || !hasAzerbaijanContext)
            variants.Add($"{normalizedAddress} Baku Azerbaijan");

        AddAzeriLetterVariants(normalizedAddress, variants);

        return variants
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddAzeriLetterVariants(string address, List<string> variants)
    {
        var latinI = address
            .Replace('İ', 'I')
            .Replace('ı', 'i');
        var azeriI = address
            .Replace('I', 'İ')
            .Replace('i', 'ı');

        if (!string.Equals(latinI, address, StringComparison.Ordinal))
        {
            variants.Add(latinI);
            variants.Add($"{latinI} Baku Azerbaijan");
        }

        if (!string.Equals(azeriI, address, StringComparison.Ordinal))
        {
            variants.Add(azeriI);
            variants.Add($"{azeriI} Bakı Azərbaycan");
        }
    }

    private static GeocodingOptions ReadOptions(IConfiguration configuration)
    {
        return new GeocodingOptions
        {
            Provider = configuration["Geocoding:Provider"],
            BaseUrl = configuration["Geocoding:BaseUrl"],
            UserAgent = configuration["Geocoding:UserAgent"],
            TimeoutSeconds = int.TryParse(configuration["Geocoding:TimeoutSeconds"], out var timeoutSeconds) ? timeoutSeconds : null,
            CacheMinutes = int.TryParse(configuration["Geocoding:CacheMinutes"], out var cacheMinutes) ? cacheMinutes : null,
            CountryCodes = configuration["Geocoding:CountryCodes"]
        };
    }

    private void ValidateProviderConfiguration()
    {
        var provider = _options.Provider?.Trim();
        var supportsRequestedProvider =
            string.IsNullOrWhiteSpace(provider)
            || string.Equals(provider, SupportedProvider, StringComparison.OrdinalIgnoreCase);

        if (!supportsRequestedProvider)
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);

        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.UserAgent)
            || !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);
        }
    }

    private string ResolveCacheProviderKey()
    {
        return SupportedProvider.ToLowerInvariant();
    }

    private int ResolveTimeoutSeconds()
    {
        return Math.Clamp(_options.TimeoutSeconds ?? MaximumTimeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds);
    }

    private int ResolveCacheMinutes()
    {
        return Math.Clamp(_options.CacheMinutes ?? MinimumCacheMinutes, MinimumCacheMinutes, MaximumCacheMinutes);
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = null!;

        public string Lat { get; set; } = null!;

        public string Lon { get; set; } = null!;

        [JsonPropertyName("place_id")]
        public JsonElement? PlaceId { get; set; }
    }

}
