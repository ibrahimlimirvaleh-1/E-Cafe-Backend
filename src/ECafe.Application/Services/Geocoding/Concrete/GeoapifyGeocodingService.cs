using System.Text.Json;
using System.Text.Json.Serialization;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.Geocoding;
using ECafe.Application.Services.Geocoding.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ECafe.Application.Services.Geocoding.Concrete;

public sealed class GeoapifyGeocodingService : IGeocodingService
{
    private const string SupportedProvider = "Geoapify";
    private const string DefaultBaseUrl = "https://api.geoapify.com";
    private const string DefaultLanguage = "az";
    private const string DefaultBias = "proximity:49.8671,40.4093";
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

    public GeoapifyGeocodingService(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        _options = ReadOptions(configuration);
    }

    public async Task<IReadOnlyList<GeocodeAddressResponse>> SearchAddressesAsync(
        string address,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedAddress = address.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAddress))
            throw new BadRequestException(ErrorCode.GeocodingAddressRequired);

        var normalizedLimit = Math.Clamp(limit, MinimumResultLimit, MaximumResultLimit);
        var cacheKey = $"geocoding:geoapify:{normalizedAddress.ToLowerInvariant()}:{normalizedLimit}";
        if (_cache.TryGetValue<IReadOnlyList<GeocodeAddressResponse>>(cacheKey, out var cached) && cached is not null)
            return cached;

        ValidateProviderConfiguration();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ResolveTimeoutSeconds()));

        try
        {
            var matches = await SearchGeoapifyAsync(normalizedAddress, normalizedLimit, timeoutCts.Token);

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

    private async Task<IReadOnlyList<GeocodeAddressResponse>> SearchGeoapifyAsync(
        string address,
        int limit,
        CancellationToken cancellationToken)
    {
        foreach (var searchAddress in BuildAddressSearchVariants(address))
        {
            var autocompleteMatches = await RequestGeoapifyAsync("autocomplete", searchAddress, limit, cancellationToken);
            if (autocompleteMatches.Count > 0)
                return autocompleteMatches;

            var searchMatches = await RequestGeoapifyAsync("search", searchAddress, limit, cancellationToken);
            if (searchMatches.Count > 0)
                return searchMatches;
        }

        throw new NotFoundException(ErrorCode.GeocodingAddressNotFound);
    }

    private async Task<IReadOnlyList<GeocodeAddressResponse>> RequestGeoapifyAsync(
        string endpoint,
        string address,
        int limit,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildGeoapifyUri(endpoint, address, limit));
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = JsonSerializer.Deserialize<GeoapifyResponse>(body, JsonOptions)?.Results ?? [];

        return results
            .Select(MapGeoapifyResult)
            .Where(result => result is not null)
            .Select(result => result!)
            .Take(limit)
            .ToList();
    }

    private Uri BuildGeoapifyUri(string endpoint, string address, int limit)
    {
        var query = new Dictionary<string, string?>
        {
            ["text"] = address,
            ["format"] = "json",
            ["limit"] = limit.ToString(),
            ["apiKey"] = _options.ApiKey!.Trim(),
            ["filter"] = ResolveCountryFilter(),
            ["bias"] = ResolveBias(),
            ["lang"] = ResolveLanguage()
        };

        var queryString = string.Join("&", query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));

        return new Uri($"{ResolveBaseUrl()}/v1/geocode/{endpoint}?{queryString}");
    }

    private static GeocodeAddressResponse? MapGeoapifyResult(GeoapifyResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Formatted))
            return null;

        return new GeocodeAddressResponse
        {
            DisplayName = result.Formatted,
            Latitude = result.Lat,
            Longitude = result.Lon,
            PlaceId = result.PlaceId
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
            ApiKey = configuration["Geocoding:ApiKey"],
            Language = configuration["Geocoding:Language"],
            Bias = configuration["Geocoding:Bias"],
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

        if (!supportsRequestedProvider
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || !Uri.TryCreate(ResolveBaseUrl(), UriKind.Absolute, out _))
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);
        }
    }

    private string ResolveBaseUrl()
    {
        return string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? DefaultBaseUrl
            : _options.BaseUrl.TrimEnd('/');
    }

    private string ResolveLanguage()
    {
        return string.IsNullOrWhiteSpace(_options.Language)
            ? DefaultLanguage
            : _options.Language.Trim();
    }

    private string ResolveBias()
    {
        return string.IsNullOrWhiteSpace(_options.Bias)
            ? DefaultBias
            : _options.Bias.Trim();
    }

    private string? ResolveCountryFilter()
    {
        return string.IsNullOrWhiteSpace(_options.CountryCodes)
            ? null
            : $"countrycode:{_options.CountryCodes.Trim().ToLowerInvariant()}";
    }

    private int ResolveTimeoutSeconds()
    {
        return Math.Clamp(_options.TimeoutSeconds ?? MaximumTimeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds);
    }

    private int ResolveCacheMinutes()
    {
        return Math.Clamp(_options.CacheMinutes ?? MinimumCacheMinutes, MinimumCacheMinutes, MaximumCacheMinutes);
    }

    private sealed class GeoapifyResponse
    {
        public List<GeoapifyResult> Results { get; set; } = [];
    }

    private sealed class GeoapifyResult
    {
        public string Formatted { get; set; } = null!;

        public double Lat { get; set; }

        public double Lon { get; set; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }
    }
}
