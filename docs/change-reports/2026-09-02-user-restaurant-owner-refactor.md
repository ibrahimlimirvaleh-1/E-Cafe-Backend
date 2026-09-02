# Change Report: UserRestaurant many-to-many və restoran sahibkar refactor

Tarix: 2026-09-02

Branch: `feature/multi-restaurant-assignments`

## Məqsəd

Restoran yaradılarkən sahibkar məlumatlarını restoran məlumatlarından ayırmaq, eyni sahibkarın bir neçə restorana bağlana bilməsini təmin etmək və bu dəyişikliyin auth/access tərəfinə təsirini hazır vəziyyətə gətirmək.

## Problem

Əvvəlki yanaşmada bu risklər var idi:

- `RegisterRestaurantRequest` içində restoran və sahibkar field-ləri eyni səviyyədə idi.
- `OwnerId == 0` sentinel kimi istifadə olunurdu.
- Yeni sahibkar yaradılarkən səhvən restoranın `Email` və `Phone` məlumatı sahibkar user-inə yazıla bilərdi.
- `UserRestaurant` cədvəlində `user_id` unique index olduğuna görə eyni user birdən çox restorana bağlana bilmirdi.
- Restoran database-ə yazılmadan əvvəl `restaurant.Id` istifadə olunurdu.
- User çox restorana bağlanandan sonra köhnə `restaurantId` claim tək restoranı təmsil etdiyi üçün access check 2-ci restoranlarda səhv bloklaya bilərdi.

## Dəyişən Backend Faylları

### `src/ECafe.Application/DTOs/Restaurant/RegisterRestaurantRequest.cs`

Owner field-ləri ayrıca nested DTO-ya çıxarıldı:

```csharp
public RegisterRestaurantOwnerRequest? Owner { get; set; }
```

Yeni DTO:

```csharp
public sealed class RegisterRestaurantOwnerRequest
{
    public int? Id { get; set; }
    public string? SearchText { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
```

Niyə:

- Restoranın öz əlaqə email/telefonu ilə sahibkar hesab məlumatları qarışmır.
- API request Swagger-də daha oxunaqlı olur.
- Gələcək autocomplete owner seçimi üçün model hazır qalır.

### `src/ECafe.Application/Features/Commands/Restaurant/RegisterRestaurantCommandValidator.cs`

Owner üçün validation əlavə edildi:

- `Owner` boş ola bilməz.
- `Owner.Id` varsa `0`-dan böyük olmalıdır.
- `Owner.Email` varsa email formatında olmalıdır.
- `Owner.Phone` varsa Azərbaycan telefon formatına uyğun olmalıdır.
- Yeni owner yaratmaq üçün email və ya email formatlı search text tələb olunur.

Niyə:

- Səhv request service logic-ə çatmadan aydın validation error ilə dayansın.
- `null` və sentinel `0` kimi qeyri-dəqiq hallar azalsın.

### `src/ECafe.Domain/Entities/User.cs`

Əvvəl:

```csharp
public virtual UserRestaurant? UserRestaurant { get; set; }
```

İndi:

```csharp
public virtual ICollection<UserRestaurant> UserRestaurants { get; set; } = new List<UserRestaurant>();
```

Niyə:

- Bir user bir neçə restorana bağlı ola bilər.
- Owner üçün çox restoran ssenarisi dəstəklənir.

### `src/ECafe.Infrastructure/Configurations/Concrete/UserRestaurantConfiguration.cs`

`user_id` unique index silindi və relation `WithMany` edildi.

Niyə:

- Database səviyyəsində eyni user-in ikinci restorana bağlanması bloklanmamalıdır.

### Migration

Yeni migration:

- `src/ECafe.Infrastructure/Migrations/20260902064837_MakeUserRestaurantManyToMany.cs`
- `src/ECafe.Infrastructure/Migrations/20260902064837_MakeUserRestaurantManyToMany.Designer.cs`
- `src/ECafe.Infrastructure/Migrations/ECafeDbContextModelSnapshot.cs`

Migration `auth.user_restaurants` üzərindən `user_restaurants_user_id_key` unique indexini drop edir.

Deploy qeydi:

- Bu migration serverdə tətbiq olunmalıdır.
- Migration işləməsə, kod hazır olsa belə database eyni user-i bir neçə restorana bağlamağa icazə verməyəcək.

### `src/ECafe.Application/Services/Restaurant/Concrete/RestaurantManager.cs`

Register flow refactor edildi:

- Sahibkar resolve logic-i `ResolveRestaurantOwnerAsync` helper-inə yığıldı.
- Mövcud sahibkar `Owner.Id` ilə seçilə bilir.
- Mövcud sahibkar `Owner.Email` ilə tapıla bilir.
- Email owner kimi tapılmasa, yeni owner yaradılır.
- Yeni owner üçün `Owner.FirstName`, `Owner.LastName`, `Owner.Phone`, `Owner.Email` istifadə olunur.
- Yeni owner-ə unusable random password hash verilir.
- Yeni owner yaradıldıqda password setup link göndərilir.
- Restoran, owner relation, audit və email outbox eyni transaction içində icra olunur.

Niyə:

- Yarımçıq restoran və ya yarımçıq owner relation yaranmasının qarşısı alınır.
- `restaurant.Id = 0` problemi aradan qalxır.
- Kod oxunaqlı helper-lərə ayrılır.

### `src/ECafe.Application/Repositories/User/IUserRepository.cs`

Yeni metod:

```csharp
Task<User?> GetOwnerByEmailAsync(string email);
```

Niyə:

- Owner seçimi exact email lookup ilə predictable olur.
- `Contains + FirstOrDefault` ambiguity yaratmır.

### `src/ECafe.Infrastructure/Repositories/User/UserRepository.cs`

`UserRestaurants` kolleksiya include/filter-ləri əlavə edildi.

`GetOwnerByEmailAsync` exact normalized email ilə işləyir.

Niyə:

- Many-to-many relation repository səviyyəsində düzgün query olunur.
- Email ilə owner resolve daha performanslı və təhlükəsizdir.

### `src/ECafe.Application/Services/Jwt/Concrete/JwtManager.cs`

JWT artıq həm köhnə, həm yeni claim verir:

- `restaurantId`: backward compatibility üçün ilk aktiv restoran id-si.
- `restaurantIds`: bütün aktiv restoran id-ləri vergüllə ayrılmış formada.

Owner də restaurant-scoped role kimi aktiv restaurant assignment tələb edir.

Niyə:

- Köhnə frontend və mövcud kod `restaurantId` ilə işləməyə davam edir.
- Yeni many-to-many access check üçün bütün restoranlar token-də olur.
- Owner restoran assignment olmadan sistemə girib restoran əməliyyatı aparmamalıdır.

### `src/ECafe.Application/Services/BaseManager.cs`

Access check yeniləndi:

- Əvvəl yalnız tək `restaurantId` yoxlanırdı.
- İndi `restaurantIds` claim parse olunur və restaurant access membership ilə yoxlanır.
- Köhnə token-lər üçün `restaurantId` fallback qalır.

Niyə:

- Bir owner-in 2-3 restoranı varsa, backend 2-ci restoran əməliyyatını səhvən bloklamasın.

### `src/ECafe.Application/Services/File/Concrete/FileAccessPolicy.cs`

File access policy də eyni claim məntiqinə uyğunlaşdırıldı.

Niyə:

- Restoran şəkli, menyu item şəkli və müqavilə faylları çox restoranlı user üçün düzgün yoxlanmalıdır.

### `src/ECafe.Infrastructure/Repositories/UserRefreshToken/UserRefreshTokenRepository.cs`

Refresh token ilə user yüklənəndə `UserRestaurant` tək navigation yerinə `UserRestaurants` kolleksiyası include olunur.

Niyə:

- Refresh token ilə yeni access token veriləndə bütün aktiv restoran assignment-lar JWT-yə düşsün.

### `src/ECafe.Application/Mappings/UserProfile.cs`

Profile mapping tək `UserRestaurant` yerinə `UserRestaurants` kolleksiyasına uyğunlaşdırıldı.

Niyə:

- Mövcud profile response qırılmasın.
- İlk aktiv restoran məlumatı backward-compatible olaraq qaytarılsın.

### `src/ECafe.Application/Services/User/Concrete/UserManager.cs`

Target user management access check kolleksiya relation-a uyğunlaşdırıldı.

Niyə:

- User-in aktiv restaurant assignment-ları kolleksiya şəklində gəldiyi üçün köhnə tək navigation compile və runtime səviyyəsində doğru deyil.

### `src/ECafe.Infrastructure/Seeders/RoleSeeder.cs`

Owner `IsStaffAssignable` siyahısından çıxarıldı.

Niyə:

- Owner personal/staff kimi idarə olunmamalıdır.
- Owner restoran sahibkarlığı üçün ayrıca konseptdir.

## Security Qeydləri

- Yeni owner yaradılarkən real password yazılmır; random unusable hash yazılır.
- Password setup flow ilə sahibkar özü password təyin etməlidir.
- Owner role olmayan user restoran sahibi kimi assign edilə bilməz.
- Deaktiv owner restoran sahibi kimi assign edilə bilməz.
- Access check artıq bütün aktiv restoran assignment-ları yoxlayır.

## Performance Qeydləri

- Owner lookup `Contains` axtarışından exact normalized email lookup-a keçirildi.
- Restaurant access check token claim üzərindən O(n) kiçik siyahı yoxlaması edir.
- Repository query-ləri `Any` ilə relation existence yoxlayır, bu database tərəfdə səmərəli SQL-ə çevrilir.

## API Payload Nümunələri

Mövcud owner-i ID ilə bağlamaq:

```json
{
  "location": "Nərimanov",
  "phone": "+994501112233",
  "email": "restaurant@mail.com",
  "restaurantGroupId": 1,
  "branchName": "Nərimanov",
  "depositAmount": 5,
  "serviceFeePercent": 10,
  "staffSettlementPeriod": 7,
  "owner": {
    "id": 2
  },
  "fileIds": [1]
}
```

Mövcud owner-i email ilə bağlamaq:

```json
{
  "location": "Nərimanov",
  "phone": "+994501112233",
  "email": "restaurant@mail.com",
  "restaurantGroupId": 1,
  "branchName": "Nərimanov",
  "depositAmount": 5,
  "serviceFeePercent": 10,
  "staffSettlementPeriod": 7,
  "owner": {
    "email": "owner@mail.com"
  },
  "fileIds": [1]
}
```

Yeni owner yaratmaq:

```json
{
  "location": "Nərimanov",
  "phone": "+994501112233",
  "email": "restaurant@mail.com",
  "restaurantGroupId": 1,
  "branchName": "Nərimanov",
  "depositAmount": 5,
  "serviceFeePercent": 10,
  "staffSettlementPeriod": 7,
  "owner": {
    "email": "new.owner@mail.com",
    "phone": "+994501234567",
    "firstName": "Tural",
    "lastName": "Seyidzadə"
  },
  "fileIds": [1]
}
```

## Test

İcra olunan command:

```bash
dotnet build ECafe.sln
```

Nəticə:

- Build succeeded
- 0 warning
- 0 error

## Qalan Qərar

Bu mərhələdə `RoleId` hələ `User` üzərində qalır. Bu o deməkdir:

- Bir user bir neçə restorana bağlana bilər.
- Amma həmin user-in rolu bütün restoranlarda eyni hesab olunur.

Əgər biznes qaydası belə olacaqsa ki, eyni adam bir restoranda `Manager`, başqa restoranda `Waiter` ola bilsin, növbəti mərhələdə `RoleId` `UserRestaurant` relation üzərinə daşınmalıdır.
