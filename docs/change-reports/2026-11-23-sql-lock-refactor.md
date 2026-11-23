# SQL kilidlərinin refaktoru

## Nə dəyişdi?

9 repository-də təkrarlanan PostgreSQL kilid SQL-ləri `Context/PostgresTransactionLocks.cs`
komponentində toplandı. Repository-lərin açıq metodları, çağırış sırası və biznes qaydaları saxlanıldı.

Komponentin üç işi var:

1. `LockUserAsync`: konkret istifadəçi sətrini `FOR UPDATE` ilə kilidləyir.
2. `LockAdvisoryAsync`: mövcud iki hissəli namespace/resource açarı ilə transaction kilidi alır.
3. `LockInventoryRowsAsync`: seçilmiş restoranın stok sətirlərini ID sırası ilə kilidləyir.

Əvvəl SQL yazılan yerdə indi məqsədi aydın olan çağırış var:

```csharp
await Context.TransactionLocks.LockUserAsync(customerUserId, cancellationToken);
```

Bu, yeni connection və ya transaction açmır. Komponent eyni `ECafeDbContext`-ə bağlıdır;
commit və rollback əvvəlki xidmətin məsuliyyətində qalır. Yeni DI müqaviləsi və ya əlavə
application interfeysi lazım olmadı. Məqsəd ümumi sorğu generatoru yaratmaq deyil, real təkrarı aradan qaldırmaqdır.

## Nələr qorundu?

- İstifadəçi sətrinin ortaq kilidi, stok transaction tələbi və boş stok siyahısı davranışı.
- Restoran, masa, mənfi müştəri/refund və tərcümə namespace açarlarının əvvəlki dəyərləri.
- İş saatı kilidinin masalardan əvvəl alınması və masaların ID sırası.
- SQL parametrləri: istifadəçi ID-si və stok ID massivi SQL mətninə birləşdirilmir.
- Cancellation token-lərinin ötürülməsi və qeyri-relational iş saatı testlərinin əvvəlki no-op davranışı.
- Restoran aidiyyəti, aktiv müştəri yoxlaması, sessiya və hesab silmə qorumaları.

İlkin mərhələdə outbox batch sorğuları və hesabatın read-only əmri dəyişdirilməmişdi.
Sonrakı tətbiq üzrə genişləndirmədə batch seçimi ortaq data-access oxuyucusuna,
read-only əmri isə DbContext köməkçi metoduna ayrıldı; sorğu davranışları saxlanıldı.
Cari sərhədlər və nəticələr `2026-11-23-application-sql-refactor.md` sənədindədir.
Migration və migrator/seeder SQL-ləri əvvəlki düzgün bootstrap sərhədində saxlanılır.

## ValidateAndThrow nə edir?

Hesab silmə axınındakı bu sətir refaktor zamanı dəyişdirilməyib:

```csharp
new CustomerAccountDeletionValidator().ValidateAndThrow(request);
```

İki ayrı addım kimi oxumaq olar:

```csharp
var validator = new CustomerAccountDeletionValidator();
validator.ValidateAndThrow(request);
```

Birinci sətir yoxlama qaydaları olan obyekt yaradır. İkinci sətir FluentValidation kitabxanasının
hazır metodudur: request-i həmin qaydalardan keçirir, pozuntu varsa `ValidationException` yaradır.
Bizim validator üç tələb qoyur:

```csharp
RuleFor(value => value.CurrentPassword).NotEmpty().MaximumLength(128);
RuleFor(value => value.Confirm).Equal(true);
```

- `CurrentPassword` boş/yalnız boşluq olmamalıdır.
- Şifrə 128 simvoldan uzun olmamalıdır.
- `Confirm` mütləq `true` olmalıdır.

Məsələn, boş şifrə və ya təsdiqlənməmiş sorğu gələndə metodda növbəti silmə addımlarına keçilmir.
Mövcud exception middleware bunu HTTP 400 validation cavabına çevirir.
Qaydalar keçirsə metod normal qayıdır və proqram davam edir; ayrıca `true` qaytarmır.

Vacib fərq: bu yoxlama şifrənin hesaba uyğunluğunu yoxlamır. `wrong-password` boş deyil və
128 simvoldan qısadırsa ilkin validation-dan keçə bilər. Sonrakı `BCrypt.Verify` isə onun
bazadakı şifrə hash-inə uyğun olub-olmadığını yoxlayır və yanlış şifrəni rədd edir.
Rol, sessiya, borc və aktiv əməliyyat yoxlamaları da ayrıca saxlanılıb.

## Yoxlama

- Build ilkin yoxlamada 0 xəta və 0 xəbərdarlıqla keçdi.
- Yeni 4 test parametrli SQL-i, köhnə kilid açarlarını, stok ID sırasını,
  real bağlantılararası gözləməni, rollback zamanı açılmanı və əvvəlki transaction/no-op qorumalarını yoxladı.
- Ümumi regresiya dəsti: **1849 keçdi, 0 uğursuz, 0 ötürülmüş test**.
  Testlər ayrıca localhost PostgreSQL 17 və Redis 7 ACL konteynerlərində işləyib;
  auth, rezervasiya, hesab silmə, stok, ödəniş, tərcümə, bildiriş və hesabat axınları daxil edilib.
- `git diff --check` keçdi. Əvvəlki 27 dirty fayldan 26-sı hash ilə dəyişməz qaldı;
  yalnız rezervasiya repository-sində bu işə aid SQL delegasiyası dəyişdi.

Əvvəlki hesab silmə işi və əlaqəsiz dəyişikliklər qorunur. Commit və push edilməyib.
