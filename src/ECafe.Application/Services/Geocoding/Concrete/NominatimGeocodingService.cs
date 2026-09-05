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
    private const string GoogleProvider = "Google";
    private const string GooglePlacesProvider = "GooglePlaces";
    private const string AutoProvider = "Auto";
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
            var matches = ShouldSearchGoogleFirst()
                ? await SearchGoogleThenNominatimAsync(normalizedAddress, normalizedLimit, timeoutCts.Token)
                : await SearchNominatimAsync(normalizedAddress, normalizedLimit, timeoutCts.Token);

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

    private async Task<IReadOnlyList<GeocodeAddressResponse>> SearchGoogleThenNominatimAsync(string address, int limit, CancellationToken cancellationToken)
    {
        if (HasGoogleConfiguration())
        {
            try
            {
                var googleMatches = await SearchGoogleAsync(address, limit, cancellationToken);
                if (googleMatches.Count > 0)
                    return googleMatches;
            }
            catch (BaseException) when (CanFallbackToNominatim())
            {
                // Nominatim remains a fallback so a Google outage or quota issue does not block admin workflows.
            }
            catch (Exception ex) when (CanFallbackToNominatim() && ex is HttpRequestException or JsonException)
            {
                // Nominatim remains a fallback so a Google outage or quota issue does not block admin workflows.
            }
        }

        return await SearchNominatimAsync(address, limit, cancellationToken);
    }

    private async Task<IReadOnlyList<GeocodeAddressResponse>> SearchGoogleAsync(string address, int limit, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildGoogleSearchUri(address));
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<GooglePlacesTextSearchResponse>(body, JsonOptions);
        if (payload is null)
            throw new ServiceUnavailableException(ErrorCode.GeocodingResponseInvalid);

        if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payload.Status, "ZERO_RESULTS", StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);
        }

        var matches = (payload.Results ?? [])
            .Select(MapGoogleResult)
            .Where(result => result is not null)
            .Select(result => result!)
            .Take(limit)
            .ToList();

        if (matches.Count == 0)
            throw new NotFoundException(ErrorCode.GeocodingAddressNotFound);

        return matches;
    }

    private async Task<IReadOnlyList<GeocodeAddressResponse>> SearchNominatimAsync(string address, int limit, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildNominatimSearchUri(address, limit));
        request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent!.Trim());

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderUnavailable);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = JsonSerializer.Deserialize<List<NominatimResult>>(body, JsonOptions) ?? [];
        var matches = results
            .Select(MapNominatimResult)
            .Where(result => result is not null)
            .Select(result => result!)
            .ToList();

        if (matches.Count == 0)
            throw new NotFoundException(ErrorCode.GeocodingAddressNotFound);

        return matches;
    }

    private Uri BuildGoogleSearchUri(string address)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.GoogleBaseUrl)
            ? "https://maps.googleapis.com/maps/api/place/textsearch/json"
            : _options.GoogleBaseUrl.Trim();
        var query = new Dictionary<string, string?>
        {
            ["query"] = address,
            ["key"] = _options.GoogleApiKey,
            ["region"] = string.IsNullOrWhiteSpace(_options.GoogleRegion) ? "az" : _options.GoogleRegion.Trim(),
            ["language"] = string.IsNullOrWhiteSpace(_options.GoogleLanguage) ? "az" : _options.GoogleLanguage.Trim()
        };

        var queryString = string.Join("&", query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}"));

        return new Uri($"{baseUrl}?{queryString}");
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

    private static GeocodeAddressResponse? MapGoogleResult(GooglePlaceResult result)
    {
        if (result.Geometry?.Location is null)
            return null;

        var displayName = string.Join(", ", new[] { result.Name, result.FormattedAddress }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(displayName))
            return null;

        return new GeocodeAddressResponse
        {
            DisplayName = displayName,
            Latitude = result.Geometry.Location.Lat,
            Longitude = result.Geometry.Location.Lng,
            PlaceId = result.PlaceId
        };
    }

    private static GeocodeAddressResponse? MapNominatimResult(NominatimResult result)
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
            GoogleApiKey = configuration["Geocoding:GoogleApiKey"],
            GoogleBaseUrl = configuration["Geocoding:GoogleBaseUrl"],
            GoogleLanguage = configuration["Geocoding:GoogleLanguage"],
            GoogleRegion = configuration["Geocoding:GoogleRegion"],
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
            || string.Equals(provider, SupportedProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, GoogleProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, GooglePlacesProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, AutoProvider, StringComparison.OrdinalIgnoreCase);

        if (!supportsRequestedProvider)
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);

        if (ShouldSearchGoogleFirst() && !HasGoogleConfiguration() && !CanFallbackToNominatim())
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);

        if (CanFallbackToNominatim()
            && (string.IsNullOrWhiteSpace(_options.BaseUrl)
                || string.IsNullOrWhiteSpace(_options.UserAgent)
                || !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _)))
        {
            throw new ServiceUnavailableException(ErrorCode.GeocodingProviderNotConfigured);
        }
    }

    private bool ShouldSearchGoogleFirst()
    {
        return string.Equals(_options.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_options.Provider, GooglePlacesProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_options.Provider, AutoProvider, StringComparison.OrdinalIgnoreCase)
            || HasGoogleConfiguration();
    }

    private bool HasGoogleConfiguration()
    {
        var googleBaseUrl = string.IsNullOrWhiteSpace(_options.GoogleBaseUrl)
            ? "https://maps.googleapis.com/maps/api/place/textsearch/json"
            : _options.GoogleBaseUrl.Trim();

        return !string.IsNullOrWhiteSpace(_options.GoogleApiKey)
            && Uri.TryCreate(googleBaseUrl, UriKind.Absolute, out _);
    }

    private bool CanFallbackToNominatim()
    {
        return string.Equals(_options.Provider, SupportedProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_options.Provider, GoogleProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_options.Provider, GooglePlacesProvider, StringComparison.OrdinalIgnoreCase)
            || string.Equals(_options.Provider, AutoProvider, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(_options.Provider);
    }

    private string ResolveCacheProviderKey()
    {
        if (ShouldSearchGoogleFirst())
            return GooglePlacesProvider.ToLowerInvariant();

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

    private sealed class GooglePlacesTextSearchResponse
    {
        public string Status { get; set; } = null!;

        public List<GooglePlaceResult>? Results { get; set; }
    }

    private sealed class GooglePlaceResult
    {
        public string? Name { get; set; }

        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }

        public GooglePlaceGeometry? Geometry { get; set; }
    }

    private sealed class GooglePlaceGeometry
    {
        public GooglePlaceLocation? Location { get; set; }
    }

    private sealed class GooglePlaceLocation
    {
        public double Lat { get; set; }

        public double Lng { get; set; }
    }
}
