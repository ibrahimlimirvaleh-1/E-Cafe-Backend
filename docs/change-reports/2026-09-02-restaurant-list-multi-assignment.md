# Multi-restaurant restoran siyahısı düzəlişi

Tarix: 2026-09-02

## Problem

Eyni istifadəçi bir neçə restorana bağlı olduqda frontend müqavilə detalını açmaq üçün əvvəlcə istifadəçinin görə bildiyi restoranların siyahısını çəkir, sonra həmin restoranların müqavilələrini yığıb contract ID ilə axtarır.

Backend `GET /api/v1/restaurants/getAll` endpointində superadmin olmayan istifadəçi üçün yalnız token-dəki ilk restoran qaytarılırdı. Buna görə iki restoranın sahibkarı olan istifadəçi yalnız birinci restoranın müqavilələrini görə bilirdi. Bildiriş Buta Baku kimi ikinci restorana aid müqaviləyə aparanda frontend həmin müqaviləni listdə tapa bilmirdi və səhifə istifadəçi üçün "Müqavilə məlumatları yüklənir..." vəziyyətində qalırdı.

## Dəyişiklik

`RestaurantManager.GetAllRestaurantsAsync` metodunda superadmin olmayan istifadəçi üçün filtr tək `GetRequiredCurrentRestaurantId()` üzərindən deyil, token-dəki bütün aktiv restoran ID-ləri üzərindən quruldu.

Əvvəl:

- yalnız birinci restoran götürülürdü;
- ikinci və sonrakı restoranlar siyahıya düşmürdü;
- həmin restoranlara aid müqavilə, menu, personal və digər admin məlumatları frontend tərəfindən dolayı yolla tapılmaya bilərdi.

İndi:

- `GetCurrentRestaurantIds()` ilə bütün icazəli restoranlar oxunur;
- restoran siyahısı `currentRestaurantIds.Contains(r.Id)` ilə süzülür;
- istifadəçi yalnız öz token-də olan restoranları görə bilir;
- superadmin davranışı dəyişməyib, bütün restoranları görə bilir.

## Dəyişən fayl

- `src/ECafe.Application/Services/Restaurant/Concrete/RestaurantManager.cs`

## Təhlükəsizlik təsiri

Dəyişiklik restoran access sərhədini geniş açmır. Əksinə, mövcud multi-restaurant modelə uyğun olaraq yalnız JWT-də olan `restaurantIds` claim-indəki restoranlar qaytarılır. Başqa restoran ID-si token-də yoxdursa siyahıya düşmür.

## Performans təsiri

Filtr SQL səviyyəsində `IN (...)` kimi işləyəcək. Tipik istifadəçidə restoran sayı az olduğu üçün performans riski aşağıdır. Superadmin üçün əvvəlki davranış saxlanılıb. Pagination, search və digər filterlər əvvəlki kimi query üzərində tətbiq olunur.

## Gözlənilən nəticə

İstifadəçi həm Dolce Vita Port Baku, həm də Buta Baku kimi birdən çox restorana bağlıdırsa:

- `GET /api/v1/restaurants/getAll` hər iki restoranı qaytarmalıdır;
- frontend contract detail açanda hər iki restoranın müqavilələrini axtara biləcək;
- Buta Baku üçün göndərilən müqavilə bildirişindən keçid zamanı müqavilə görünməlidir.

## Yoxlama planı

1. İki restorana bağlı sahibkar hesabı ilə sistemə daxil ol.
2. `GET /api/v1/restaurants/getAll` response-da hər iki restoranın gəldiyini yoxla.
3. İkinci restorana aid müqaviləni sahibkara təsdiq üçün göndər.
4. Sahibkar hesabında bildirişə kliklə.
5. Müqavilə detal səhifəsinin açıldığını və "Müqavilə məlumatları yüklənir..." vəziyyətində qalmadığını yoxla.

