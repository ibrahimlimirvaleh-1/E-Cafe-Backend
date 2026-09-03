# 2026-09-03 - App daxili bildirişlərin restoran kontekstinə bağlanması

## Məqsəd

Eyni istifadəçi bir neçə restoranda fərqli profilə sahib olduqda app daxili bildirişlər yalnız aktiv seçilmiş restoran kontekstinə uyğun görünməlidir. Məsələn istifadəçi Buta Baku profilindədirsə, Dolce Vita Port Baku üçün göndərilmiş müqavilə bildirişi həmin profildə görünməməlidir.

## Dəyişdirilən fayllar

- `src/ECafe.Application/Repositories/Notification/INotificationRepository.cs`
- `src/ECafe.Infrastructure/Repositories/Notification/NotificationRepository.cs`
- `src/ECafe.Application/Services/Notification/Concrete/NotificationManager.cs`

## Görülən işlər

### Repository metodlarına restoran scope-u əlavə edildi

Bildiriş siyahısı, oxunmamış bildiriş sayı, oxunmamış bildirişlərin oxunmuş edilməsi və tək bildirişin oxunmuş edilməsi artıq `restaurantId` parametrini qəbul edir.

Filter qaydası:

- `UserId` mütləq cari istifadəçi olmalıdır.
- Əgər restoran scope-u varsa, `RestaurantId` həmin restoran olmalıdır.
- `RestaurantId = null` olan ümumi hesab/sessiya bildirişləri saxlanılır və bütün profillərdə görünə bilər.

Bu yanaşma security baxımından vacibdir, çünki frontend gizlətsə belə, backend başqa restoranın bildirişini qaytarmır və oxunmuş etməyə icazə vermir.

### NotificationManager aktiv restoran kontekstindən istifadə edir

Non-admin istifadəçilər üçün aktiv restoran `X-Active-Restaurant-Id` header-i və token claim-ləri əsasında götürülür. Platforma super administratoru üçün əvvəlki davranış qorunur, yəni admin bütün restoran bildirişlərini görə bilər.

## Biznes təsiri

- Sahibkar/menecer/ofisiant/mətbəx istifadəçisi yalnız aktiv profilinə uyğun restoran bildirişlərini görür.
- Bir istifadəçinin fərqli restoran profilləri bir-birinin müqavilə, stok və digər restoran-spesifik bildirişlərini qarışdırmır.
- Ümumi hesab bildirişləri restoran profilindən asılı olmayaraq görünə bilər.

## Təhlükəsizlik qeydi

Bu düzəliş yalnız UI filter deyil. Əsas məhdudiyyət backend query-lərində tətbiq edilir. Buna görə istifadəçi DevTools və ya manual API çağırışı ilə başqa restoran scope-undakı bildirişi oxumağa və ya oxunmuş etməyə çalışsa, həmin bildiriş ona qaytarılmayacaq.

## Test tövsiyələri

1. Eyni istifadəçini iki restorana sahibkar kimi əlavə et.
2. Birinci restoran üçün müqavilə bildirişi yarat.
3. İstifadəçi ikinci restoran profili ilə daxil olduqda həmin bildirişin bell-də və `/notifications` səhifəsində görünmədiyini yoxla.
4. Birinci restoran profilinə keçdikdə bildirişin göründüyünü yoxla.
5. İkinci restoran profilində birinci restoran bildirişini `mark-as-read` endpoint-i ilə oxunmuş etməyə çalış və backend-in onu tapmadığını yoxla.
