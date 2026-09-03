# Profil response-da giriş profilləri

Tarix: 2026-09-02

## Məqsəd

Sistemdə bir istifadəçi bir neçə restorana bağlı ola və hər restoranda fərqli rol daşıya bilər. Bu modeldə profil səhifəsində yalnız bir `RestaurantId` və bir `Role` göstərmək istifadəçini çaşdırırdı.

Məqsəd `/api/v1/profile` response-da istifadəçinin bütün aktiv restoran-rol profillərini qaytarmaqdır.

## Dəyişikliklər

### `ProfileResponseDto`

Fayl:

- `src/ECafe.Application/DTOs/User/ProfileResponseDto.cs`

Əlavə edildi:

- `Profiles`
- `UserProfileAssignmentDto`

Hər profil bu məlumatları qaytarır:

- `RestaurantId`
- `RestaurantName`
- `RoleId`
- `RoleName`
- `IsActive`

### `UserManager.MapToProfileResponseAsync`

Fayl:

- `src/ECafe.Application/Services/User/Concrete/UserManager.cs`

İstifadəçinin `UserRestaurants` relation-larından aktiv olanlar seçilir, restoran adı və rol adı ilə birlikdə response-a yazılır.

## Niyə belə edildi?

Bu yanaşma backend-də mövcud many-to-many `UserRestaurant` modelinə uyğundur. Frontend artıq bir istifadəçini tək rol/tək restoran kimi yox, bir neçə giriş profili kimi göstərə bilər.

## Təhlükəsizlik təsiri

Response yalnız cari autentifikasiya olunmuş istifadəçinin öz profil məlumatıdır. Başqa istifadəçinin restoran-rol əlaqələri açılmır.

## Uyğunluq

Köhnə frontend üçün `RestaurantId`, `RestaurantName`, `RoleId`, `Role` field-ləri saxlanılıb. Yeni frontend `Profiles` massivindən istifadə edir.

## Yoxlama planı

1. Birdən çox restorana bağlı istifadəçi ilə login ol.
2. `GET /api/v1/profile` çağır.
3. `profiles` massivində bütün aktiv restoran-rol cütlərinin gəldiyini yoxla.
4. Tək restoranlı istifadəçidə massivdə bir element gəldiyini yoxla.

