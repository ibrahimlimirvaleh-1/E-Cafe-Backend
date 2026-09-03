# Aktiv profil restoran sərhədi

Tarix: 2026-09-03

## Problem

Bir istifadəçi bir neçə restorana bağlı ola bilər. Məsələn istifadəçi Buta Baku profilini aktiv seçəndə Dolce Vita Port Baku məlumatlarını da görə bilirdi. Bu biznes və təhlükəsizlik baxımından yanlışdır. Platform admin bütün restoranları görə bilər, amma restoran rolları yalnız hazırda seçilən aktiv restoran kontekstində işləməlidir.

## Dəyişikliklər

### Aktiv restoran header-i

Fayllar:

- `src/ECafe.Application/Services/BaseManager.cs`
- `src/ECafe.Infrastructure/Authorization/RestaurantContextAuthorizationHelper.cs`
- `src/ECafe.Application/Services/File/Concrete/FileAccessPolicy.cs`

Backend `X-Active-Restaurant-Id` header-ini oxuyur. Bu header frontend-də seçilən aktiv profil restoranını bildirir.

Qayda:

- Platform admin üçün bütün restoranlara icazə qalır.
- Qeyri-admin istifadəçi üçün aktiv restoran konteksti əsas götürülür.
- Sorğudakı restoran aktiv kontekstdən fərqlidirsə `403 Forbidden` qaytarılır.

### Restoran siyahısı scope-u

Fayl:

- `src/ECafe.Application/Services/Restaurant/Concrete/RestaurantManager.cs`

`GetAllRestaurantsAsync` qeyri-admin istifadəçilər üçün artıq token-də olan bütün restoranları qaytarmır. Yalnız aktiv profil restoranını qaytarır.

## Niyə belə edildi?

Frontend-də sadəcə filter etmək təhlükəsizlik üçün kifayət deyil. İstifadəçi browser DevTools və ya Postman ilə başqa restoran ID-si göndərə bilər. Ona görə əsas məhdudiyyət backend servis qatında tətbiq edildi.

Bu yanaşma həm UX-i, həm də authorization modelini eyni edir:

- Header-də profil dəyişirsən.
- Frontend həmin aktiv profili API header-i kimi göndərir.
- Backend həmin aktiv profil kontekstindən kənar restoran məlumatını bloklayır.

## Performans təsiri

Əlavə database sorğusu əlavə olunmadı. Backend aktiv restoranı request header və JWT claim-lərindən oxuyur. Mövcud permission handler onsuz da restoran-role yoxlaması üçün cache-dən istifadə edir.

## Təhlükəsizlik təsiri

Bu dəyişiklik restoranlararası məlumat sızmasının qarşısını alır. Bir istifadəçinin iki restorana təyinatı olsa belə, hazırda hansı profillə işləyirsə yalnız həmin restoran kontekstində data görə və əməliyyat edə bilər.

## Yoxlama planı

1. İki restorana bağlı sahibkar/menecer istifadəçi ilə login ol.
2. Header-dən Buta Baku profilini seç.
3. `/api/v1/restaurants/getAll` çağır və yalnız Buta Baku gəldiyini yoxla.
4. Eyni aktiv profillə Dolce Vita detail endpoint-inə sorğu göndər və `403` aldığını yoxla.
5. Header-dən Dolce Vita profilinə keç.
6. Dolce Vita məlumatlarının gəldiyini, Buta Baku detail sorğusunun isə bloklandığını yoxla.
7. Platform admin ilə login olub bütün restoranların göründüyünü yoxla.
