# Restoran profilinin deaktiv edilməsi

> Bu sənəd əvvəlki şəxsi/müştəri fallback yanaşmasının tarixçəsidir. Son işçi profili bağlananda çıxış və təkrar girişin rədd edilməsi üçün qüvvədə olan yeni qayda: [2026-11-22 hesabatı](2026-11-22-staff-profile-required-login.md).

## Problem

Eyni istifadəçinin Buta Baku və Dolce Vita profilləri var. Bir restoran profilinin deaktiv edilməsi digər restorandakı sessiyanı da bağlayırdı.

Videolarda həm başqa restoranın personal statusunun dəyişməsi, həm də digər restoran açıq olarkən girişdən çıxarılma müşahidə edildi.

Üç səbəb aradan qaldırılır:

1. `UpdateStaffRequest.IsActive` mapper vasitəsilə ümumi `User.IsActive` sahəsinə yazılırdı.
2. Profilin deaktiv edilməsi ümumi `SessionVersion` artırır, bütün cihazların refresh tokenlərini ləğv edirdi.
3. `UserDeactivated` hadisəsi bütün hesabı çıxarırdı; frontend də restoran icazəsi hadisəsində səhifəni reload edirdi və boş profil siyahısını köhnə JWT-dən bərpa edirdi.

## Yeni Qayda

- `User.IsActive` hesab səviyyəsində bloklamadır. Restoran profilinin dəyişdirilməsi bunu dəyişmir.
- `UserRestaurant.IsActive` yalnız həmin restoran profilinin statusudur.
- Ayrı deaktiv düyməsi və personal redaktə formasındakı status sahəsi eyni sərhədi saxlayır.
- Yeni personal yaradılarkən status seçimi restoran profilinə yazılır; şəxsi hesab aktiv yaradılır. Deaktiv profil şəxsi hesabı bloklamır.
- Bu yaradılma qaydası yalnız sahibkar, menecer, ofisiant və mətbəx rollarına aiddir. Qlobal administrator/müştəri hesabının yaradılma statusuna aid əvvəlki davranış qorunur.
- Profil dəyişməsi `SessionVersion` və cihaz tokenlərini dəyişmir.
- Deaktiv edilmiş restorana köhnə JWT ilə gələn yeni sorğunun icazəsi bazadakı cari aidiyyətə əsasən rədd edilir.
- Digər aktiv restoranlar, şəxsi hesab və cihaz sessiyaları işləməyə davam edir.
- Son restoran profili deaktiv ediləndə yeni token və profil cavabı yalnız müştəri rolunu verir. Köhnə personal icazələri və restoran claim-ləri həmin tokenə daxil edilmir. Saxlanmış hesab rolu dəyişdirilmir.
- Şəxsi hesaba keçid eyni DB policy ilə mobil tətbiqə giriş və mətbəx/push bildirişlərində də tanınır. Öz sifariş/rezervasiya/refund/sessiya aidiyyəti yenə yoxlanılır; köhnə işçi hadisələri şəxsi bildiriş kimi göndərilmir.
- Profilin aktivləşdirilməsi hesab səviyyəsində bloklamanı aça bilməz. Bu halda `409 StaffAccountInactive (1140)` qaytarılır.

## Server Axını

1. Cari istifadəçinin restoran səlahiyyəti, hədəf profil və sahibkar/superadmin məhdudiyyətləri yoxlanılır.
2. Transaction açılır və istifadəçi sətri kilidlənir. Refresh və paralel profil dəyişməsi eyni kilidi gözləyir.
3. Yalnız seçilmiş aidiyyətin statusu dəyişir. Redaktədə mövcud e-poçt təsdiqi və fayl aidiyyəti qorumaları saxlanır.
4. Audit və dəyişikliklər saxlanıb transaction commit edilir.
5. Yalnız həmin istifadəçi/restoran aidiyyəti keşinin invalidasiyası edilir.
6. İstifadəçiyə mövcud `RestaurantAccessChanged` hadisəsi göndərilir: `staff_activated` və ya `staff_deactivated`. `restaurantId` dəyişən profili göstərir; ümumi logout hadisəsi göndərilmir.

## Frontend İnteqrasiyası

- Aktiv profillər üçün `GET /profile` cavabı əsas mənbədir. Boş siyahı köhnə JWT ilə doldurulmamalıdır.
- Başqa restoranın profili bağlananda cari profil, URL və açıq ekran saxlanmalıdır.
- Cari profil bağlananda qalan aktiv profilin uyğun əsas ekranına keçilməlidir. Profil qalmadıqda şəxsi hesab açıq saxlanmalıdır.
- Köhnə restoranın məlumatları və əməliyyatları yeni profilə daşınmamalıdır.
- Realtime hadisə, reconnect, focus və periodik yoxlama eyni reconciliation qaydasını istifadə etməlidir.
- Hesab həqiqətən bloklanarsa əvvəlki qlobal sessiya qorumaları və logout qalır.

## Avtomatik Yoxlamalar

- Ayrı deaktiv əmri və redaktə yolu: tək və iki restoranlı hesab.
- Yeni personalın aktiv/deaktiv profillə yaradılması; hər iki halda şəxsi hesabın aktiv qalması.
- Digər restoran profilinin, ümumi aktiv statusun, sessiya versiyasının və refresh tokenlərin qorunması.
- Hadisənin yalnız dəyişən restoranı göstərməsi; ümumi deaktiv hadisəsinin göndərilməməsi.
- Öz profilini deaktiv etmə, başqa restoranı idarə etmə və yüksək rol qorumaları.
- Son profil bağlananda müştəri tokeni və boş profil cavabı; personal hüquqlarının sızmaması.
- Şəxsi fallback ilə mobil giriş, öz mətbəx bildirişi və push; başqa müştərinin və əlaqəsiz köhnə işçi bildirişinin göndərilməməsi.
- Real PostgreSQL-də köhnə tenant claim-ləri ilə rədd, digər tenant və digər cihazla uğurlu giriş.
- Real PostgreSQL-də refresh/deaktiv və aktiv/deaktiv yarışlarının hər iki ardıcıllığı.
- Yazı xətasında rollback və vaxtından əvvəl realtime hadisəsinin olmaması.
- Həqiqi hesab bloklamasının refresh-i rədd etməsi və profil aktivləşdirməsi ilə açılmaması.

## Deploydan Sonra Qısa Test

1. Eyni hesab üçün A və B restoranlarında aktiv profil hazırlayın. Ayrı browser kontekstlərində A və B-ni açın.
2. Administratorla A profilini deaktiv edin. Həm ayrıca düyməni, həm də redaktə formasını ayrı sınaqlarda yoxlayın.
3. B ekranı açıq qalsın: logout, tam reload və məcburi əsas səhifəyə keçid olmamalıdır. B-də yeni icazəli sorğu işləməlidir.
4. A açıq olan browser qalan B profilinə keçsin; A-nın köhnə məlumat və düymələri görünməsin.
5. Köhnə A tokeni ilə A-ya yeni idarəetmə sorğusu rədd edilsin. Bu yoxlamanı yalnız UI düyməsinə baxmaqla əvəz etməyin.
6. A profilini aktiv edin. A yenidən profil siyahısında görünsün; B açıq qalsın.
7. Son aktiv profili də bağlayın. Şəxsi hesab açıq, restoran idarəetmə hüquqları isə bağlı qalsın. Logout/login və refresh də şəxsi hesab kimi işləsin.
8. Realtime əlaqəni test mühitində kəsib profil dəyişin. Əlaqə bərpa olunanda və ya focus/periodik yoxlamada eyni nəticə alınsın.
9. Həqiqətən bloklanmış test hesabının restoran profilini aktivləşdirmək bloklamanı açmasın. Parol bərpası, təsdiqlənmiş e-poçt dəyişikliyi və bütün cihazlardan çıxış əvvəlki qorumaları saxlasın.
10. Deaktiv statusla yeni personal yaradın: şəxsi hesab aktiv, həmin restoran profili deaktiv olsun. Son profil bağlandıqdan sonra şəxsi sifarişə mətbəx bildirişi işləsin; əvvəlki işçi hadisəsi və başqa müştərinin obyekti üçün push göndərilməsin.

## Yoxlama Nəticələri

- Backend geniş regresiya: 1 761/1 761, skip yoxdur. Bu icra mobil şəxsi bildiriş uyğunlaşdırmasından əvvəlki profil düzəlişlərini tam əhatə edir.
- Son mobil/şəxsi hesab uyğunlaşdırmasından sonra 140/140 fokus testi, skip yoxdur: mobil qeydiyyat və göndəriş sərhədləri, mətbəx, profilin deaktiv edilməsi, real PostgreSQL şəxsi hesab filtri və müştəri sifarişinin tam mətbəx axını.
- Əlavə fokus icrasında profil, yaradılma statusu, yarışlar və tərcümə kataloqu üzrə 89/89 test keçib.
- Frontend: 27 brauzer ssenarisi, 16 profil qaydası, əvvəlki 16 auth yarışı və 20 e-poçt ssenarisi keçib; TypeScript/Vite build, i18n və diff yoxlamaları uğurludur.
- Frontend testləri həqiqi SignalR client-i ilə saxta HTTP/WebSocket üzərində işləyib. 22 desktop/mobil/dark/light görüntü yoxlanılıb; `StaffAccountInactive` rədd mesajı danger tonundadır.
- Backend inteqrasiya testləri ayrıca loopback PostgreSQL/Redis konteynerlərindən istifadə edib. Push nəqliyyatı saxtadır; real telefona göndəriş və canlı backend/frontend E2E iddiası edilmir.
- Backend və frontenddə `git diff --check` uğurludur. Əvvəlki əlaqəsiz lokal dəyişikliklər bu işin commitlərinə daxil edilmir.

## Köhnə Qeydlər Və Əhatə

Bu iş əvvəlki səhv əməliyyatlarla `User.IsActive=false` olmuş hesabları avtomatik açmır. Qəsdən bloklanmış hesabı səhvən açmamaq üçün həmin qeydlərin səbəbi və audit tarixçəsi administrator tərəfindən ayrıca yoxlanmalıdır.

Yeni DB sütunu və migration yoxdur. Qlobal hesab silinməsi/bloklanması, şifrə və təsdiqlənmiş e-poçt dəyişməsinin bütün sessiyaları bağlayan qorumaları dəyişdirilməyib. Rol dəyişmə əməliyyatının ayrıca mövcud davranışı bu işdə dəyişdirilmir.

Canlı serverə və istifadəçi məlumatlarına toxunulmur. Əvvəlki lokal sənədləşdirmə dəyişiklikləri və `IUserRestaurantAccessCache.cs` rəyindəki boşluq qorunur. Hazırlıq mərhələsində commit/push edilməyib; dəyişikliyin commit və push edilməsi istifadəçinin ayrıca təsdiqi ilə icra olunur. Merge və deploy bu işin əhatəsinə daxil deyil.
