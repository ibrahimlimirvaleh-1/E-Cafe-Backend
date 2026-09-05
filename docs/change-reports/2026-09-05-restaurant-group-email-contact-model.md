# Restoran qrupu email əlaqə modeli

## Məqsəd

Restoran filialının ayrıca email saxlaması biznes məntiqini qarışdırırdı: eyni sahibkar və ya eyni biznes qrupu bir neçə filiala sahib ola bilər, amma rəsmi əlaqə emaili çox vaxt filialdan yox, brend/restoran qrupundan idarə olunur. Bu dəyişikliklə rəsmi email tam olaraq `RestaurantGroup` səviyyəsinə daşındı və `Restaurant.Email` modeldən çıxarıldı.

## Dəyişən kodlar

- `src/ECafe.Domain/Entities/RestaurantGroup.cs`
  - `Email` sahəsi əlavə model kimi saxlanıldı və nullable edildi.
  - Nullable saxlanmasının səbəbi mövcud datanı deploy zamanı qırmamaqdır; yeni əməliyyatlarda validator email tələb edir.

- `src/ECafe.Domain/Entities/Restaurant.cs`
  - `Email` property-si çıxarıldı.
  - Filialın özündə email saxlanılmır; əlaqə emaili restoran qrupuna aiddir.

- `src/ECafe.Infrastructure/Configurations/Concrete/RestaurantConfiguration.cs`
  - `restaurants.email` mapping-i və email index davranışı silindi.

- `src/ECafe.Infrastructure/Configurations/Concrete/RestaurantGroupConfiguration.cs`
  - `RestaurantGroup.Email` üçün `email` sütun adı təyin olundu.
  - Email üzərində non-unique index əlavə edildi: `ix_restaurant_groups_email`.
  - Index unique deyil, çünki eyni əlaqə emaili bir neçə biznes qrupu üçün istifadə oluna bilər.

- `src/ECafe.Infrastructure/Migrations/20260905062413_MoveRestaurantEmailToGroup.cs`
  - `core.restaurant_groups.email` sütunu əlavə edildi.
  - Mövcud `restaurants.email` dəyərləri mümkün olduqda bağlı qrup emailinə daşındı.
  - `core.restaurants.email` sütunu silindi.
  - `restaurant_groups.email` üçün index yaradıldı.

- `src/ECafe.Application/DTOs/RestaurantGroup/CreateRestaurantGroupRequest.cs`
  - Qrup yaradılarkən `Email` qəbul edilir.

- `src/ECafe.Application/DTOs/RestaurantGroup/RestaurantGroupResponse.cs`
  - Qrup siyahısı response-unda `Email` qaytarılır.

- `src/ECafe.Application/Features/Commands/RestaurantGroup/CreateRestaurantGroupCommandValidator.cs`
  - Yeni qrup yaradanda `Name` və `Email` tələb edilir.
  - `EmailAddress()` format yoxlaması əlavə edildi.

- `src/ECafe.Application/Services/RestaurantGroup/Concrete/RestaurantGroupManager.cs`
  - Qrup emaili normalize edilir: trim + lowercase.
  - Yeni qrup yaradılarkən email saxlanılır.
  - Qrup siyahısında email response-a map edilir.

- `src/ECafe.Application/DTOs/Restaurant/RegisterRestaurantRequest.cs`
  - Filial `Email` sahəsi çıxarıldı.
  - Yeni `RestaurantGroupEmail` sahəsi əlavə edildi.

- `src/ECafe.Application/DTOs/Restaurant/UpdateRestaurantRequest.cs`
  - Filial `Email` sahəsi çıxarıldı.
  - Yeni `RestaurantGroupEmail` sahəsi əlavə edildi.

- `src/ECafe.Application/Features/Commands/Restaurant/RegisterRestaurantCommandValidator.cs`
  - Yeni qrup yaradılarkən `RestaurantGroupEmail` tələb olunur və email formatı yoxlanılır.

- `src/ECafe.Application/Features/Commands/Restaurant/UpdateRestaurantCommandValidator.cs`
  - Edit zamanı yeni qrup yaradılırsa `RestaurantGroupEmail` tələb olunur.

- `src/ECafe.Application/Services/Restaurant/Concrete/RestaurantManager.cs`
  - Restoran duplicate yoxlamasından email çıxarıldı.
  - Duplicate yoxlaması restoran adı/filial adı və telefon üzərində qalır.
  - Yeni qrup yaradılarkən `RestaurantGroupEmail` group entity-yə yazılır.
  - Restoran qeydiyyatı email bildirişi artıq qrup emailinə göndərilir.

- `src/ECafe.Application/Mappings/RestaurantProfile.cs`
  - Restoran detail response-da `RestaurantGroupEmail` dəyəri qrup emailindən götürülür.

- `src/ECafe.Application/Services/RestaurantContract/Concrete/ContractFileService.cs`
  - Müqavilə sənədində email qrup emailindən götürülür.

## Təsir

- Restoran filialında email saxlanılmır.
- Rəsmi restoran əlaqəsi qrup səviyyəsində idarə olunur.
- Köhnə datada qrup emaili olmayan qeydlər deploy zamanı sistemi qırmır.
- Yeni qrup yaratma əməliyyatlarında email tələb olunduğu üçün yeni data təmiz modelə uyğun gəlir.

## Yoxlama

- `dotnet build` uğurla keçdi.
- EF migration uğurla yaradıldı.
