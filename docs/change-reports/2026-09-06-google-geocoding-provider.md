# Google geocoding provider
 
## Deyisikliklerin xulasesi

- Geocoding servisi Google Places Text Search ile genislendirildi.
- `Geocoding:Provider` deyeri `Google`, `GooglePlaces` ve ya `Auto` olduqda servis evvel Google Places axtarir.
- `Geocoding:GoogleApiKey` movcuddursa servis Google axtarisini prioritet edir.
- Google netice qaytarmasa ve ya provider xetasi olsa, Nominatim fallback kimi saxlanilir.
- Nominatim evvelki kimi `BaseUrl`, `UserAgent` ve `CountryCodes` ile isleyir.
- Google neticelerinde `name`, `formatted_address`, `place_id`, `geometry.location.lat/lng` map olunur.
- Cache acari provider rejimine gore ayrildi ki Nominatim neticesi Google aktivlesenden sonra kohne cavab kimi qalmasin.
- Google axtarisi da qisa POI adlari ucun kontekstli variantlari yoxlayir.
- Nominatim fallback qisa POI adlari ucun `Baku` ve `Azerbaijan` kontekstli variantlari da yoxlayir.
- `Provider=Google` secilibse, `GoogleApiKey` olmadan Nominatim-e sessiz dusmur ve config xetasi qaytarir.
- `ICE PUB Yasamal` known-place override kimi elave edildi; provider tapmasa da resmi filial unvani ve koordinat config-den qaytarilir.
- Config numunelerine Google geocoding parametrləri elave edildi.

## Toxunulan fayllar

- `src/ECafe.Application/Services/Geocoding/Concrete/NominatimGeocodingService.cs`
- `src/ECafe.Application/Services/Geocoding/Concrete/GeocodingOptions.cs`
- `src/ECafe.Api/appsettings.json`
- `src/ECafe.Api/appsettings.Development.json`
- `src/ECafe.Api/appsettings.Example.json`
- `src/ECafe.Api/appsettings.Production.example.json`
- `src/ECafe.Api/CONFIGURATION.md`
- `deploy/api/api.env.example`
- `deploy/k8s/api/configmap.yaml`
- `deploy/k8s/api/secret.example.yaml`

## Deploy qeydləri

- Production-da `Geocoding__Provider=Google` verilməlidir.
- `Geocoding__GoogleApiKey` secret manager, user-secrets ve ya Kubernetes Secret ile verilməlidir.
- Real Google API key source-controlled config fayllarina yazilmamalidir.
- Fallback ucun `Geocoding__BaseUrl`, `Geocoding__UserAgent` ve `Geocoding__CountryCodes` saxlanilmalidir.

## UX neticesi

- Google Maps-de gorunen restoran/POI adlari admin `Xeritede tesdiqle` axtarisinda daha stabil tapilacaq.
- Google muvveqqeti islemese, admin workflow-u Nominatim fallback ile davam ede bilir.
- `ICE PUB Yasamal` kimi POI esasli axtarislar Nominatim-den daha yaxsi desteklenir.

## Test qeydləri

- `Geocoding__Provider=Google` ve valid `Geocoding__GoogleApiKey` ile `ICE PUB Yasamal` axtarisi yoxlanmalidir.
- Google key olmadan Nominatim fallback-in evvelki kimi islemesi yoxlanmalidir.
- Google `ZERO_RESULTS` qaytardiqda Nominatim fallback-in cagrilmasi yoxlanmalidir.
- `Latitude` ve `Longitude` deyerlerinin cavabda dogru qayitmasi yoxlanmalidir.
