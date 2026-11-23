# Tətbiq üzrə SQL refaktoru

Bu sənəd əvvəlki `2026-11-23-sql-lock-refactor.md` işinin genişləndirilməsidir.
İlk mərhələdə kilid SQL-ləri birləşdirilmişdi; indi bütün runtime SQL çağırışları audit olunub
və data-access/DbContext sərhədlərində saxlanılır. Biznes qaydaları və endpoint-lər dəyişdirilməyib.

## Hazır olan işlər

- [x] API, Application, Domain, Shared, Infrastructure və migrator üzrə birbaşa SQL çağırışlarını yoxlamaq.
- [x] Təkrarlanan kilid SQL-lərini ortaq `PostgresTransactionLocks` komponentində saxlamaq.
- [x] Realtime və mobil push batch seçimini ortaq `OutboxBatchReader`-ə ayırmaq.
- [x] Hesabatın PostgreSQL read-only əmrini DbContext köməkçi metoduna ayırmaq.
- [x] InMemory yollarını, izlənən sətirləri, transaction və paralel worker davranışını ayrıca test etmək.
- [x] Ümumi regresiya dəstinin yekun nəticəsini qeyd etmək.

## Runtime SQL Haradadır?

| Yer | Məsuliyyət |
| --- | --- |
| `Infrastructure/Context/PostgresTransactionLocks.cs` | İstifadəçi sətri, iki hissəli advisory kilidi, restoranla məhdudlaşan stok sətirləri. |
| `Infrastructure/Repositories/Outbox/OutboxBatchReader.cs` | İzlənən realtime və mobile-push batch-lərinin ayrı qaydalarla seçilməsi. |
| `Infrastructure/Context/ECafeDbContext.Transactions.cs` | Mövcud PostgreSQL hesabat transaction-ını yalnız oxuma rejiminə keçirmək. |

API/Application/Domain/Shared və Infrastructure worker xidmətlərində birbaşa SQL icrası yoxdur.
Adi sorğuların mövcud LINQ forması saxlanılıb. Məqsəd bütün LINQ sorğularını yenidən yazmaq
və ya PostgreSQL-in xüsusi əmrlərini süni şəkildə LINQ-ə çevirmək deyil.

## Outbox-da Nə Dəyişdi?

Əvvəl iki çatdırılma emalçısının içində SQL və ayrıca InMemory LINQ seçimi vardı.
İndi emalçı hazır batch-i oxuyucudan alır:

```csharp
var events = await _outboxBatches.ReadRealtimeAsync(batchSize, now, token);
var events = await _outboxBatches.ReadMobilePushAsync(batchSize, cancellationToken);
```

Eyni DbContext və transaction istifadə edilir. Seçilən sətirlər izlənən qalır;
emalçının `ProcessedAt`, retry və digər dəyişiklikləri əvvəlki `SaveChanges` ilə yazılır.
Oxuyucu transaction açmır, commit etmir və bildiriş göndərmir.
Processor konstruktorlarının və DI qeydiyyatının mövcud müqavilələri dəyişməyib.

İki axının fərqi qəsdən saxlanılıb:

- Realtime: yalnız iki realtime event tipi, işlənməmiş status və vaxtı çatmış `LockedUntil`.
- Mobile push: yalnız mobile-push event tipi və işlənməmiş status; realtime retry filtri tətbiq edilmir.
- PostgreSQL-də əvvəlki `ORDER BY OccurredAt, Id`, batch həddi və `FOR UPDATE SKIP LOCKED` saxlanılır.
- InMemory yolunun əvvəlki filtrləri və sıralaması dəyişdirilmir.
- Retry, idempotentlik, çatdırılma icazələri, token qorunması və başqa restoranın məlumatından təcrid qaydaları emalçılarda qalır.

## Hesabatda Nə Dəyişdi?

```csharp
await db.SetCurrentTransactionReadOnlyAsync(cancellationToken);
```

Bu metod artıq açılmış transaction üçün PostgreSQL əmrini icra edir.
`RepeatableRead` başlanğıcı, commit/dispose və hesabatın restoran/rol filtrləri dəyişdirilmir.
Başqa provayderdə əvvəlki no-op davranışı saxlanılır.

## Qəsdən Dəyişdirilməyən SQL

- Migration və migration seed-ləri: əvvəllər tətbiq edilmiş baza tarixçəsi olduğu üçün yenidən yazılmır.
- Dev migrator seed-i: sabit ilkin test məlumatı yaradan idempotent SQL düzgün bootstrap sərhədindədir.
- `deploy/dev-data/seed-demo-data.sql`: runtime biznes kodu deyil, ayrıca dev məlumat skriptidir.
- Testlərdəki diaqnostik SQL: gerçək bazada qorumaları yoxlamaq üçün saxlanılır.

Bu istisnalar təhlükəsiz parametrli runtime SQL-in xidmət qatlarına geri yayılması demək deyil.
SQL-i sadəcə böyük `QueryConstants` faylına yığmaq, dinamik cədvəl adları və ya lazımsız query-builder əlavə etməkdən istifadə olunmayıb.

## Yoxlama

- Yeni 4 test PostgreSQL və InMemory batch filtrlərini, parametrləri, tracking-i,
  paralel batch-lərin ayrılığını, rollback ilə sətirlərin açılmasını və hesabat transaction rejimini yoxlayıb.
- Fokus testləri: 4 keçdi, 0 uğursuz, 0 ötürülmüş test.
- Ümumi regresiya nəticəsi: **1853 keçdi, 0 uğursuz, 0 ötürülmüş test**.
  Mövcud auth/sessiya, icazə/restoran sərhədləri, hesab silmə, rezervasiya,
  sifariş, stok, ödəniş, tərcümə, hesabat və bildiriş sınaqları da keçib.
- `git diff --check` keçdi. Əvvəlki 39 dirty fayldan 38-i hash ilə dəyişməzdir;
  yalnız əlaqəli SQL refaktor hesabatına bu genişləndirmə barədə qeyd əlavə edilib.
- Yoxlamalar ayrıca localhost PostgreSQL/Redis mühitində aparılır; canlı serverə dəyişiklik edilmir.

Əvvəlki hesab silmə işi və əlaqəsiz dəyişikliklər qorunur. Commit/push edilməyib.
