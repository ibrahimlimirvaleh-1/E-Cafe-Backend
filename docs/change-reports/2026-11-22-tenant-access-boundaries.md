# Restoranlararasi Melumat Ve Icaze Serhedleri

## Task Siyahisi

| ID | Task | Prioritet | Veziyyet |
| --- | --- | --- | --- |
| SEC-TENANT-01 | Umumi hesab silinmesini platforma administratoru ile mehdudlasdir | Yuksek | Hazirdir: backend, frontend ve avtomatik testler |
| SEC-TENANT-02 | Bildirislerde canli restoran aidiyyetini ve profil deyisme serhedini qoru | Orta | Hazirdir: backend, frontend ve avtomatik testler |
| SEC-TENANT-03 | Restoran qruplarinin idareetme siyahisini yalniz platforma adminine ac | Orta | Hazirdir: backend, frontend ve avtomatik testler |

## SEC-TENANT-01

Evvel `ManageStaff` icazesi olan restoran meneceri kohne `DELETE /users/{id}` yolu ile ortaq hesabi silinmis isareleyib diger restoranlarin sessiyalarini da baglaya bilirdi.

- Endpoint indi platforma administratoru rolunu (`1`) ve `ManageUsers` icazesini birlikde teleb edir.
- `UserManager.DeleteAsync` birbasa cagirildiqda da restoran rollarini transaction, token legvi ve hesab yazisindan evvel redd edir.
- Admin ucun soft delete, session version artimi, butun cihaz tokenlerinin legvi, kes temizliyi ve realtime cixis saxlanir.
- Restoran rehberliyi ucun ayrica restoran profili deaktiv/aktiv emeliyyatlari deyismir.
- Frontendde umumi sil duymesi yalniz platforma adminine gorunur ve butun restoran girislerine tesiri ayrica tesdiq penceresinde gosterilir; tesadufi iki defe gonderilme qorumasi var.

## SEC-TENANT-02

Evvel A restoran profili baglanib B aciq qalanda kohne JWT ile A restoraninin oz bildiris tarixcesi secile bilirdi. Bu, basqa istifadecinin bildirislerine giris deyildi; legv edilmis restoran girisinin qalmasi idi.

- List, unread count, tek oxunmus isareleme ve hamisini oxunmus isareleme canli restoran rolunu bazadan yoxlayir.
- Repository-de de bildiris sorqusunun ozune aktiv hesab, aktiv restoran, aktiv aidiyyet ve restoran isci rolu sertleri daxildir. Xidmetin ilkin yoxlamasi ile SQL arasinda aidiyyet legv olunsa kohne tarixce qaytarilmir.
- Bos restoran scope-u isciye butun restoranlari acmir. Id ve restoran header-i bildiris sahibinin istifadeci ID-sini evez etmir.
- Adi musterinin ve platforma administratorunun yalniz oz bildirisleri muxtelif restoranlardan gorune bilir; onlar ucun isci aidiyyeti teleb olunmur.
- Yeni bildiris, e-post, mobil push ve realtime outbox yaratma mentiqi deyisdirilmir.
- Frontendde list, say, oxuma emeliyyati ve yonlendirme cari profil/sessiya ile baglanir. A-dan gec gelen netice B-ni ve ya yeniden acilmis A-ni deyismir; A-nin pending veziyyeti B-ni kilidlemir. Profil deyisende gozleyen mutasiya legv edilir ki, token refresh-den sonraki avtomatik tekrar B restoranina gonderilmesin.
- Restoran aidiyyeti ucun 403 yalniz hemin bildiris gorunusunu temizleyir; basqa aktiv profili olan hesabi umumi logout etmir.

## SEC-TENANT-03

Evvel adi musteriye de verilmis `ViewRestaurantInfo` icazesi butun aktiv qruplarin huquqi adini ve e-postunu qaytarirdi.

- GET/POST `/restaurant-groups` platforma administratoru rolu ve `ManageRestaurants` icazesi ile qorunur.
- Xidmetin hem siyahi, hem yaratma metodu restoran rollarini ve musterini birbasa da redd edir.
- Adminin aktiv qruplari siyahilamasi, qrup yaratmasi ve restoran formalarinda qrup secmesi saxlanir.
- Ictimai restoran cavablarinda movcud qrup adi/ID-si deyisdirilmir; yeni ictimai huquqi/e-post kataloqu elave edilmir.
- Owner/manager restoran ekraninda lazimsiz qrup siyahisi sorgusu yoxdur; adminin siyahi ve secim axini saxlanir.

## Avtomatik Yoxlama

- Ilkin fokus icrasi: 98 kecdi, ugursuz ve skip yoxdur.
- Real loopback PostgreSQL fokus icrasi: 4 kecdi, ugursuz ve skip yoxdur. Buraya ayri baglantida profil legvinden sonra bildiris SQL-inin girisi redd etmesi ve adi musteri/admin tarixcesinin yalniz oz sahibine aciq qalmasi daxildir.
- Genis suite: 1 812 testden 1 798-i kecdi, ugursuz test yoxdur. Ayrica dashboard connection deyiseni verilmeyen 14 PostgreSQL testi buraxildi.
- Son build yeni UI ehtiyat kataloqu daxil olmaqla 0 xeberdarliq ve 0 xeta ile kecdi. Dashboard connection da ayrica loopback klasterine verildikden sonra 70/70 yekun yoxlama kecdi, skip yoxdur: buraxilmis 14 dashboard testi, 34 tercume testi, 20 yeni tenant xidmet testi ve 2 yeni PostgreSQL bildiris testi.
- Belelikle genis suite-deki butun 1 812 test ferqli icralar uzre yoxlanildi. Ilk genis icrada 0 skip oldugu iddia edilmir.
- 7 yeni UI acari AZ/EN/RU ile `tenant-access-ui.server.json` ehtiyat kataloquna daxil edildi; frontend handoff fayli ile deqiq uygunlugu yoxlanildi.
- Frontend: 33/33 tenant brauzer ssenarisi, 6/6 komponent-remount olmadan bildiris yarisi, 36/36 auth refresh, 20/20 profil qaydasi, 46/46 personal profili brauzer ssenarisi ve 22/22 e-post tehlukesizliyi ssenarisi kecib: cemisi 163.
- TypeScript/Vite build ve 2 393 AZ/EN/RU acari/placeholder uygunlugu kecib. Vite movcud iri chunk ve ucuncu teref SignalR annotation xeberdarliqlarini saxlayir; backend build-de xeberdarliq yoxdur.
- AZ desktop/light, RU 320px/dark silme tesdiqi ve 320px bildiris icaze mesaji vizual olaraq yoxlanilib. Browser testleri saxta HTTP/WebSocket ve real SignalR client istifade edir; canli E2E, SMTP ve real telefon push testi deyil.

## Deploydan Sonra Qebul Testleri

1. Eyni iscinin A ve B restoran profillerini aciq saxlayin. A meneceri ile umumi DELETE sorgusu 403 vermeli; hesab, B profili ve cihaz sessiyalari deyismemelidir.
2. Platforma admini ile ayrica test hesabini silin. Hesab ve butun restoran/cihaz girisleri baglanmalidir.
3. A profilini baglayib B-ni aciq saxlayin. Kohne token/header ile A bildiris siyahisi, unread, read ve read-all redd edilmelidir; B bildirisleri islemelidir.
4. Aktiv A profilinde B-nin ve basqa istifadecinin bildiris ID-si ile read sorgusu melumat deyismemelidir.
5. Adi musteri restoran profili olmadan oz A/B rezervasiya ve sifaris bildirislerini oxuya bilmelidir; basqasinin bildirisini yox.
6. Owner, manager, waiter, kitchen ve customer ile `/restaurant-groups` oxumasi/yazmasi 403 vermeli; admin siyahi ve yaratma axini islemelidir.
7. Restoran/profil ve hesab deyiserken gecikmis bildiris cavabi onceki restoranin melumatini, unread sayini ve yonlendirmesini yeni profile dasimamalidir.

## Ehate

Yeni migration ve yeni permission ID-si yoxdur. Canli server, SMTP, real telefona push ve production melumatlari deyisdirilmir. Evvelki elaqesiz senedler ve `IUserRestaurantAccessCache.cs` rey boslugu qorunur. Bu isde commit, push, merge ve deploy edilmir.
