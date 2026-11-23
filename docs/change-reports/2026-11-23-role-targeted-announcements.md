# Rollara uyğun məhsul yenilikləri

## Məqsəd və davranış

İstifadəçi ayrıca yeniliklər səhifəsinə girmədən, girişdən və cari profil seçildikdən sonra özünə aid yenilikləri görür. Bu mexanizm sifariş, mətbəx və ödəniş bildirişlərindən ayrıdır; push bildirişi göndərmir.

- İş axınını bağlamayan kiçik non-modal panel istifadə olunur.
- Profil seçimi, açıq modal və həssas əməliyyat zamanı avtomatik panel gözləyir.
- Bir avtomatik qrupda maksimum üç görünməmiş elan var. Tək kart üzrə açıq istifadəçi hərəkəti yalnız həmin kartın versiyasını görülmüş edir.
- Görülmə qeydi bazada istifadəçi + rol + elan + versiya ilə saxlanır. Digər cihazda və eyni rolun başqa restoranında yenidən məcburi göstərilmir.
- Müxtəlif rol seçildikdə həmin rolun uyğun, görülməmiş elanları ayrıca seçilir.
- Müştəri üçün hesab silmə elanı aktiv işçi profili olan hesaba göstərilmir.

Yanaşmanın mənbələri: [Fluent onboarding](https://fluent2.microsoft.design/onboarding) və [Carbon notification patterns](https://v10.carbondesignsystem.com/patterns/notification-pattern/). Bunlar kontekstual, işi kəsməyən təqdimatı dəstəkləyir; bu layihəyə tətbiq edilən konkret qaydalar yuxarıda göstərilib.

## Backend quruluşu

`ProductAnnouncementManager` biznes qaydalarını, `ProductAnnouncementRepository` sorğuları, `ProductAnnouncementPolicy` isə qapalı keçid/rol/icazə xəritəsini saxlayır. Ümumi notification/push və sessiya keşinin mövcud davranışı dəyişmir.

Bazanın `platform` sxemində dörd yeni cədvəl var: `product_announcements`, `product_announcement_texts`, `product_announcement_roles`, `product_announcement_receipts`. İlkin hesab silmə nümunəsi ID 1 ilə **draft** yaradılır; yeni ID ardıcıllığı 2-dən başlayır.

API marşrutları `/api/v1` altındadır:

- `GET /announcements/pending`: cari profil üçün maksimum üç kart, `roleId`, `restaurantId`, `hasMore`.
- `POST /announcements/{id}/acknowledge`: `{version}`; uğurlu və idempotent cavab 204.
- `GET /admin/announcements?beforeId=...`: maksimum 30 admin qeydi və cursor.
- `GET /admin/announcements/destinations`: dəstəklənən keçidlər və rollar.
- `POST /admin/announcements`: draft yaratmaq.
- `PUT /admin/announcements/{id}`: yalnız draft-ı `expectedVersion` ilə yeniləmək.
- `POST /admin/announcements/{id}/publish` və `/unpublish`: `expectedVersion` ilə yayımı dəyişmək.

Admin əməliyyatları yalnız qlobal platforma admininə açıqdır. Restoran sahibkarı/meneceri platforma elanını yayımlaya bilmir.

## Qorumalar

1. JWT istifadəçi ID-si ilə yanaşı hesabın aktivliyi, qlobal rol, sessiya versiyası və cihaz sessiyası bazadan yoxlanır.
2. Seçilmiş rol JWT-dəki qlobal rolla qarışdırılmır. İşçi profili üçün restoran və rolun canlı aidiyyəti ayrıca yoxlanır. Saxta header səlahiyyət vermir.
3. Keçid icazələri canlı `RolePermission` məlumatına əsaslanır; köhnə Redis/JWT icazəsi uyğun olmayan kartı açmır.
4. Sərbəst URL və HTML yoxdur. Müştəri yalnız serverin tanıdığı sabit destination açarını alır.
5. Draft üç dil sətri ilə natamam saxlanıla bilər. Yayım üçün AZ/EN/RU başlıq və mətnləri tamamlanmalı, markup/dəyişən yoxlamasından keçməlidir. Başlıq 120, mətn 600 simvoldur.
6. Yayımlanmış elan birbaşa redaktə olunmur. Əvvəl yayımdan çıxarılır, draft saxlananda versiya artır. Dəyişmədən yayımı dayandırıb başlatmaq görülmüş elanı təkrar göstərmir.
7. Yazı əməliyyatları transaction daxilində istifadəçi kilidi, sonra elan üzrə advisory kilidlə ardıcıllaşır. `expectedVersion` köhnə admin formasının yeni məzmunu əvəz etməsini dayandırır.
8. Receipt kompozit açarı və kilid paralel təkrar sorğularda bir qeyd saxlayır. Əvvəl saxlanmış receipt üçün sonradan yayım dayansa da retry uğurludur.
9. Pending feed bir SQL snapshot-dan oxunur; yayımdan çıxarma/redaktə zamanı draft mətnlərinin köhnə published məlumatı ilə qarışması önlənir.
10. İstifadəçi üzrə rate limit 60 sorğu/dəqiqədir; admin body 16 KB, receipt body 1 KB ilə məhduddur. API cavabları `no-store` istifadə edir.
11. Auditə əməliyyat/metaməlumat yazılır, elan mətni və şəxsi məlumatlar kopyalanmır.

## Yoxlama və yayım

Tam backend sınağı: **1882 passed, 0 failed, 0 skipped**, müddət 6 dəqiqə 11 saniyə. Buraya əvvəlki hesab silmə, SQL refaktoru, sessiya/icazə, outbox, real-time, inventar və biznes testləri də daxildir. Testlər yalnız ayrıca loopback PostgreSQL/Redis və mocked frontend API ilə aparılır; canlı server dəyişdirilmir.

Yeni testlər rol 1–6 xəritəsini, saxta profil/cihaz və köhnə sessiyanı, canlı icazə dəyişikliyini, üç dili, bounded feed və admin pagination-ı, paralel receipt və iki ayrı adminin köhnə versiya yarışını, ack/unpublish yarışının hər iki sırasını, rollback və migration up/down davranışını yoxlayır. Migration testi mövcud istifadəçi və cihaz sessiyalarının qorunduğunu, draft seed-i və yeni identity-nin 2-dən başladığını təsdiqləyir.

UI üçün **49 AZ/EN/RU açarı**, API xətaları üçün əlavə dörd açar mərkəzi backend kataloquna daxil edilib. Bu resurslarla və son same-role/cross-restaurant receipt, `no-store` yoxlamaları ilə təkrar sınaq: **88 passed, 0 failed, 0 skipped** (yeniliklər + tərcümə kataloqu + struktur + .NET uyğunluğu). `Release` solution build: **0 warning, 0 error**. `git diff --check` təmizdir.

Yalnız bu iş üçün açılan lokal PostgreSQL/Redis konteynerləri və sintetik ACL credential faylları testdən sonra silinib. Əvvəlki 46 dirty faylın hash müqayisəsində yalnız bu funksiyanın bilərəkdən inteqrasiya etdiyi ortaq fayllar dəyişib; əvvəlki dörd əlaqəsiz fayl dəqiq əvvəlki məzmunda qalıb. Git index boş, HEAD `c801030`, backend branch `feat/role-targeted-announcements` olaraq qalır.

Frontend `feat/role-targeted-announcements-ui` branch-ində, ayrıca `frontend-role-announcements` iş ağacında tamamlanıb:

- Yeniliklər: **58 browser + 6 development StrictMode ssenarisi keçdi**.
- Mövcud auth/access/deaktivlik/e-poçt/tenant/bildiriş sınaqları: **163 ssenari keçdi**.
- Mövcud dashboard/hesabat sınağı keçdi. Köhnə sahibkar mock-una həqiqi aktiv profil əlavə edilib; tətbiqin profilsiz personalı bloklayan qaydası dəyişməyib.
- Əvvəlki ayrıca hesab silmə iş ağacında **51 ssenari** də keçdi. Bu, iki frontend funksiyasının birləşdirilmiş E2E testi deyil; əvvəlki dəyişikliklər hələ həmin ayrıca iş ağacındadır.
- TypeScript və Vite build keçdi. Əvvəldən olan SignalR PURE annotation və böyük bundle xəbərdarlıqları qalır.
- **2442 AZ/EN/RU açarı** uyğunluq yoxlamasından keçdi; bu işin 49 açarı backend resursu ilə üç dildə eynidir.
- Panel və admin üçün 320/390/1440 px, üç dil, light/dark üzrə **36 screenshot** yaradılıb; üfüqi daşma yoxlanıb. AZ desktop/admin, AZ mobile və RU dark mobile görüntüləri bu chatda da ayrıca vizual yoxlanıb.
- Yeni draft-ın `updatedAtUtc: null` müqaviləsi, React StrictMode abort/retry, canlı auth/scope/route guard-ları və birbaşa Reports tabına təkrar CTA keçidi ayrıca yoxlanıb.

Frontendin geniş yekunu `frontend-role-announcements/docs/role-targeted-announcements-ui.md` faylındadır. Lokal preview `http://127.0.0.1:5243/` yalnız lokal `127.0.0.1:8080` backend hədəfi ilə açıqdır; burada backend başladılmayıb və production proxy yoxdur. Mocked screenshot `http://127.0.0.1:5243/dist/announcement-az-1440-light.png` ünvanında baxıla bilər. Normal interaktiv giriş üçün uyğun ayrıca lokal backend lazımdır.

Yayım ardıcıllığı: backend migration və API, sonra uyğun frontend. Daha sonra platforma admini draft elanını yoxlayıb ayrıca yayımlamalıdır. Hesab silmə nümunəsi yalnız hesab silmə funksiyasının hər iki tərəfi yayımlandıqdan sonra yayımlanmalıdır.

Bu backend işi əvvəlki, hələ commit olunmamış SQL refaktorunun `PostgresTransactionLocks` komponentindən istifadə edir. Gələcək commit/PR-lar ayrılarsa, həmin SQL refaktoru əvvəl gələn asılılıq kimi saxlanmalıdır; təkcə yeni elan repository-sini götürmək kifayət deyil. Hesab silmə UI-si frontendin əvvəlki iş ağacında ayrıca qorunur; onun hələ yayımlanmaması elanın draft qalmasının əlavə səbəbidir.

Əl ilə qəbul sınağı:

1. Admin olaraq üç dildə draft yaradın; adi istifadəçidə görünməməlidir.
2. Elanı bir rola yayımlayın; həmin rolda giriş/profil seçimi bitdikdə panel görünsün, başqa rolda görünməsin.
3. Açıq form/modal zamanı panel işi kəsməsin; bağlandıqdan sonra göstərilsin.
4. Kartı bağlayın və ya keçidini açın; yenidən girişdə/digər cihazda həmin versiya təkrar avtomatik açılmasın.
5. Bir neçə kart varsa, bir kartın bağlanması digərlərini görülmüş etməsin.
6. Admin yayımlanmış məzmunu redaktə edə bilməsin; unpublish, edit/save, publish etdikdə yeni versiya yenidən uyğun auditoriyaya görünsün.
7. AZ/EN/RU, telefon/desktop, klaviatura və dark mode görünüşlərini yoxlayın.
8. İcazəni/profili dəyişəndə köhnə cavab və köhnə kart digər profilə daşınmasın.

Commit, push, merge, deploy və APK hazırlanması bu işin icazəsinə daxil deyil və icra edilmir. Əvvəlki hesab silmə/SQL refaktoru və əlaqəsiz lokal dəyişikliklər qorunur.
