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
        var cacheKey = $"geocoding:{normalizedAddress.ToLowerInvariant()}:{normalizedLimit}";
        if (_cache.TryGetValue<IReadOnlyList<GeocodeAddressResponse>>(cacheKey, out var cached) && cached is not null)
            return cached;

        ValidateProviderConfiguration();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(ResolveTimeoutSeconds()));
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSearchUri(normalizedAddress, normalizedLimit));
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent!.Trim());

        try
        {
            using var response = await HttpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
                throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<List<NominatimResult>>(body, JsonOptions) ?? [];
            var matches = results
                .Select(MapResult)
                .Where(result => result is not null)
                .Select(result => result!)
                .ToList();

            if (matches.Count == 0)
                throw new NotFoundException(ErrorCode.GeocodingAddressNotFound);

            _cache.Set(cacheKey, matches, TimeSpan.FromMinutes(ResolveCacheMinutes()));
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

    private Uri BuildSearchUri(string address, int limit)
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

    private static GeocodeAddressResponse? MapResult(NominatimResult result)
    {
        if (string.IsNullOrWhiteSpace(result.DisplayName)
            || !double.TryParse(result.Lat, out var latitude)
            || !double.TryParse(result.Lon, out var longitude))
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
        if (!string.Equals(_options.Provider, SupportedProvider, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.UserAgent)
            || !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);
        }
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
