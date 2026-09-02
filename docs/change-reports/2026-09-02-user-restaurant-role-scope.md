# UserRestaurant Rol Scope Refaktoru

Tarix: 2026-09-02
Branch: feature/multi-restaurant-assignments

## Məqsəd

Restoran əməkdaşlarının rolları artıq yalnız global istifadəçi məlumatından oxunmur; rol restoran assignment-i üzərində saxlanılır. Bu yanaşma eyni şəxsin bir neçə restorana təyin olunmasını və hər restoranda fərqli əməliyyat roluna sahib olmasını dəstəkləyir.

Nümunə:
- İstifadəçi A Restoran 1-də Sahibkar ola bilər.
- Eyni İstifadəçi A Restoran 2-də Menecer ola bilər.
- Staff siyahıları, sahibkar yoxlamaları və workflow rol yoxlamaları seçilmiş restorana aid roldan istifadə etməlidir.

## Niyə Bu Daha Düzgündür

Əvvəl `auth.users.role_id` yeganə rol mənbəyi idi. Bu, istifadəçi yalnız bir restorana aid olanda işləyir, amma many-to-many restoran assignment dəstəyindən sonra qeyri-müəyyənlik yaradır.

Yeni yanaşmada restoran daxilindəki rollar üçün əsas mənbə `auth.user_restaurants.role_id` olur. Mövcud login və permission axını qırılmasın deyə `auth.users.role_id` compatibility üçün saxlanılıb.

## Database Dəyişiklikləri

`auth.user_restaurants` cədvəlinə `role_id` əlavə olundu.

Migration ardıcıllığı:
- Əvvəl `role_id` nullable kimi əlavə olunur.
- Mövcud assignment-lər `auth.users.role_id` dəyərindən doldurulur.
- Gözlənilməz null sətirlər üçün fallback olaraq Sahibkar rolu tətbiq olunur.
- Sonra `role_id` not-null edilir.
- `user_restaurants_role_id_idx` index-i əlavə olunur.
- `auth.roles(id)` cədvəlinə `user_restaurants_role_id_fkey` foreign key-i əlavə olunur.

Bu ardıcıllıq deploy zamanı mövcud datanın qırılmasının qarşısını alır.

## Kod Dəyişiklikləri

### Domain

`UserRestaurant` entity-sinə əlavə olundu:
- `RoleId`
- `Role`

`Role` entity-sinə əlavə olundu:
- `UserRestaurants`

### EF Configuration

`UserRestaurantConfiguration` indi bunları map edir:
- `role_id` column-u
- role index-i
- role foreign key-i

### Repositories

`UserRestaurantRepository` staff və sahibkar assignment-lərini artıq `UserRestaurant.RoleId` ilə filter edir.

Dəyişən davranış:
- Restoran sahibkarı lookup-u assignment rolundan istifadə edir.
- Staff lookup-u Customer və SuperAdmin rollarını assignment roluna görə istisna edir.
- Role-based restoran assignment query-ləri assignment rolundan istifadə edir.

### Mapping

Staff DTO-larında rol məlumatı artıq assignment-dən map olunur:
- `UserRestaurant.RoleId`
- `UserRestaurant.Role.Name`

Bu, eyni istifadəçinin fərqli restoranlarda fərqli rolları olduqda API cavablarının düzgün qalmasını təmin edir.

### JWT

JWT-yə `restaurantRoles` claim-i əlavə olundu:

```text
restaurantRoles=3:2,5:3
```

Format:
- sol tərəf: restoran id
- sağ tərəf: həmin restorandakı role id

Mövcud claim-lər saxlanılıb:
- `role`
- `roleName`
- `restaurantId`
- `restaurantIds`

Bu, cari client-ləri qırmadan restoran-scope rol yoxlamalarını mümkün edir.

### Workflow

Workflow action yoxlamaları `restaurantId` mövcud olduqda həmin restoran üçün təyin edilmiş roldan istifadə edir.

Bu, eyni istifadəçinin fərqli restoranlarda fərqli rolları olduqda workflow action-ların yalnız global rola görə səhv görünməsinin qarşısını alır.

### Authorization

`PermissionAuthorizationHandler` restaurant-context-aware edildi.

Yeni davranış:
- Request-də `restaurantId` varsa, permission yoxlaması həmin restoran üçün DB-dəki aktiv `UserRestaurant` assignment-indən oxunan role-id ilə aparılır.
- SuperAdmin global icazə ilə əvvəlki kimi bütün sistem səviyyəli əməliyyatları görə bilir.
- İstifadəçi request-dəki restorana assignment olunmayıbsa, permission uğurlu sayılmır və request bloklanır.
- Assignment, istifadəçi və ya restoran deaktivdirsə, request bloklanır. Bu access token-in qısa müddət stale qalmasından yarana biləcək boşluğu bağlayır.
- Request-də `restaurantId` yoxdursa, global endpoint-lər əvvəlki kimi əsas `role` claim-i ilə işləyir.

Bu dəyişiklik Tural kimi istifadəçilərdə bu vəziyyəti bağlayır:
- Tural X restoranında Sahibkardır.
- Tural Y restoranında Menecerdir.
- Tural X context-i ilə Y restoranının rəhbər səlahiyyətli əməliyyatlarını görə və icra edə bilməz.

`ActiveRestaurantContractAuthorizationHandler` də eyni restaurant id resolver-i istifadə edir. Bununla route/query/legacy claim oxuma məntiqi bir helper-də mərkəzləşdirildi və təkrar kod azaldıldı.

Əlavə olunan helper:
- `RestaurantContextAuthorizationHelper` route, query və legacy claim üzərindən `restaurantId` tapır.
- Helper eyni parsing qaydasını authorization handler-lər arasında paylaşır.

Əlavə olunan repository metodu:
- `GetActiveRoleIdAsync(userId, restaurantId)` yalnız aktiv user, aktiv restoran və aktiv assignment olduqda həmin restoran üçün role-id qaytarır.
- Bu metod permission handler-də istifadə olunur və stale token riskini azaldır.

### Restaurant Access Cache

Permission yoxlamasında hər request-də DB-yə getməmək üçün `UserRestaurantAccessCache` əlavə olundu.

Cache açarı:

```text
user-restaurant-access:{userId}:{restaurantId}
```

Cache davranışı:
- Əvvəl cache oxunur.
- Cache miss olduqda DB-dən aktiv assignment role-id oxunur.
- Nəticə qısa TTL ilə cache-ə yazılır.
- Default TTL 2 dəqiqədir.
- Config dəyəri `Auth:RestaurantAccessCacheMinutes` ilə idarə olunur.
- Dəyər 1-15 dəqiqə aralığında məhdudlaşdırılır.

No-access nəticəsi də qısa müddətə cache-lənir. Bu, olmayan assignment-lər üçün təkrar DB sorğularını azaldır.

Cache invalidasiya olunan hallar:
- Yeni staff yaradıldıqda.
- Staff aktivləşdirildikdə.
- Staff deaktivləşdirildikdə.
- Staff məlumatları yeniləndikdə.
- İstifadəçinin global role/session məlumatı dəyişdikdə.
- İstifadəçi silindikdə.
- Restoran yaradılıb sahibkar assignment-i əlavə edildikdə.
- Restoran deaktiv edildikdə həmin restorana bağlı user-lər üçün.

Bu yanaşma təhlükəsizlik və performans balansını qoruyur: permission yoxlamaları sürətli olur, amma kritik dəyişikliklərdən sonra köhnə access məlumatı saxlanmır.

### User Service

Staff aktivləşdirmə, deaktivləşdirmə, yeniləmə, detail və siyahı axınları restoran context-i olduqda assignment rolundan istifadə edir.

## Compatibility Qeydləri

`auth.users.role_id` bu dəyişiklikdə bilərəkdən silinməyib. Cari login və köhnə client uyğunluğu üçün əsas role claim hələ saxlanılır.

Növbəti təhlükəsiz addım:
- Global rolları yalnız SuperAdmin və Customer kimi sistem səviyyəli identity-lər üçün saxlamaq.
- Permission claim-lərini də gələcəkdə restoran context-indən dinamik hesablamaq.

## Yoxlama

İcra olundu:

```powershell
dotnet build ECafe.sln
```

Nəticə:
- Build uğurla keçdi.
- 0 warning.
- 0 error.
