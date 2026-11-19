# Sessiya və icazə keşinin köhnəlmə riski

Tarix: 20.11.2026. Branch: `fix/authorization-cache-consistency`.

## Problem nə idi?

Əvvəl Redis-də istifadəçinin aktivliyi/sessiya versiyası, cihaz sessiyasının
aktivliyi, restoran rolu və rol icazələri saxlanırdı. Bazada çıxış ləğv edilsə də,
Redis açarının silinməsi alınmaya bilərdi. Paralel köhnə oxunuş da silinmiş açarı
yenidən yaza bilərdi. Nəticədə əvvəlki “icazə var” cavabı TTL bitənədək qala bilərdi.

Məsələn, ofisiant deaktiv edilib, amma keşdə hələ “aktivdir” yazılıb. Təkcə TTL-i
qısaltmaq bu problemi bağlamır; yalnız riskin müddətini azaldır.

## İndi necə işləyir?

- Sessiyanın aktivliyi və versiyası hər yoxlamada cari bazadan oxunur.
- Cihaz sessiyasının ləğvi və bitmə vaxtı bazadan yoxlanır.
- Restoran rolu istifadəçi/restoran/aidiyyət aktivliyi ilə birlikdə bazadan gəlir.
- Rol icazələri `AsNoTracking` sorğusu ilə bazadan oxunur; köhnə Redis siyahısı qəbul edilmir.
- Təhlükəsizlik oxunuşları bu Redis açarlarını nə oxuyur, nə də yenidən doldurur.
- Köhnə açarları silən çağırışlar və interceptor uyğunluq üçün saxlanır. Redis
  nasazlığı bazada tamamlanmış ləğvi xəta kimi göstərmir; məxfi provayder mətni
  çıxarılmadan xəbərdarlıq yazılır. Ləğvetmə və proqramlaşdırma xətaları gizlədilmir.
- Baza cavab vermirsə, köhnə keşə əsaslanıb giriş verilmir. Redis-in işləməsi
  cari baza yoxlamasını əvəz etmir.

`*Cache` interfeys/sinif adları mövcud çağırışları dəyişməmək üçün saxlanıb.
Əvvəlki `Auth:SessionStateCacheMinutes` və `Auth:RestaurantAccessCacheMinutes`
ayarları artıq təhlükəsizlik qərarının müddətini müəyyən etmir. Redis SignalR
backplane-i və paylanmış bağlantı limitləri üçün işləməyə davam edir.

## Nələr dəyişmir?

JWT formatı, refresh rotasiyası, istifadəçi sətrinin DB kilidləri, transaction
sərhədləri, restoran sərhədləri, parol bərpa/təyin qaydaları və cihazdan çıxışın
digər cihazı bağlamaması qorunur. API cavabları və frontend protokolu dəyişmir.
Yeni DB cədvəli, miqrasiya, paket və ya canlı server ayarı tələb olunmur.

## Dəqiqlik sərhədi

Zəmanət: ləğv bazada commit olunduqdan sonra başlayan təhlükəsizlik oxunuşu
köhnə Redis məlumatı ilə girişi bərpa edə bilməz. Başqa instansiyanın sonradan
köhnə açarı yazması da bu qərarı dəyişmir.

Bu, commit-dən əvvəl icazə almış və artıq işləyən hər əməliyyatı avtomatik geri
qaytarmaq demək deyil. Həssas yazıların mövcud transaction/kilid qorumaları
saxlanıb. Əvvəldən açılmış WebSocket-i ani kəsmək və artıq göndərilmiş bildirişi
geri çəkmək ayrıca mexanizmdir; bu dəyişiklik belə bir zəmanət vermir.

## Sorğu xərci

Sıfır pəncərəli ləğv yoxlaması üçün Redis əvəzinə cari baza sorğuları tələb olunur.
Tipik cihaz sessiyalı restoran icazəsi yoxlaması ən çox dörd proyeksiya sorğusu
işlədir. Test bu həddi, tracking-siz oxumanı və parol/token hash sütunlarının
oxunmadığını yoxlayır. Baza bağlantısı yazıların olduğu aktual primary mənbəyə
getməlidir; gecikən read-replica bu təhlükəsizlik zəmanətinin əvəzi deyil.

## Yoxlamalar

- Köhnə müsbət/mənfi və səhv JSON keş cavabları cari baza qərarını dəyişmir.
- Deaktiv hesab, yeni sessiya versiyası, ləğv edilmiş/bitmiş cihaz sessiyası rədd edilir.
- Restoran rolunun dəyişməsi və aidiyyətin alınması dərhal yeni oxunuşda görünür.
- Baza xətası köhnə “icazə var” məlumatı ilə uğurlu cavaba çevrilmir.
- Real Redis dayandırılır, həmin vaxt icazə bazada alınır, Redis bərpa edilir və
  köhnə cavablar qəsdən yenidən yazılır. Köhnə icazə/sessiya yenə qəbul edilmir.
- Real PostgreSQL və ayrıca test Redis-də altı ləğv ssenarisinin hər birində
  gecikmiş keş yazısından sonra 16 ayrı DB/API scope-u paralel yoxlanır.
- Bir restoranın və bir cihazın ləğvi digər restoran/cihazı əsassız bağlamır.
- Explicit transaction rollback olduqda əvvəlki qanuni icazə saxlanır.
- Sinxron/asinxron SaveChanges zamanı Redis silmə xətası bazadakı ləğvi pozmur.

Yeni test nəticələri repository xaricindəki lokal `auth-cache-*` qovluqlarına
yazılır. Testlər yalnız ayrıca loopback PostgreSQL/Redis konteynerlərinə qoşulur;
canlı serverdə Redis dayandırılmır və istifadəçi məlumatı dəyişdirilmir.

Yekun Release build: **0 xəbərdarlıq, 0 xəta**. Tam regresiya paketi:
**1702 keçdi, 0 xəta, 0 buraxılmış test**. Əsas nəticə faylı lokal iş qovluğunda
`auth-cache-full-results/*.trx`-dir. Altı paralel ssenari real PostgreSQL və
`127.0.0.1:16393`-dəki ayrıca test Redis-i ilə işlədilib. Redis ACL hesablarının
və həqiqi Redis dayanma/bərpa sınağının şərtləri də bu keçiddə aktiv olub.
Köhnə açarların həqiqətən doldurulması ilə sərtləşdirilmiş testdən sonra əlaqəli
**50 fokus testi də ayrıca keçdi** (0 xəta, 0 buraxılmış test).

## Deploy və yoxlama

1. Bütün API instansiyalarını eyni yeni versiyaya keçir; köhnə binarilər keşə
   etibar etməyə davam edir. Qarışıq versiya dövrünü riskin tam bağlanması sayma.
2. Mövcud Redis ACL-ləri, `.env`, JWT açarları və data-protection volume-u saxla.
   Redis-i flush etmə, portu açma və qoruma qaydalarını zəiflətmə.
3. İki test brauzerində normal login/refresh yoxla. Bir cihazdan çıxış digərini
   bağlamamalı; bütün cihazlardan çıxış/parol bərpası köhnə sessiyaları rədd etməlidir.
4. Test əməkdaşının bir restoran aidiyyətini və ya icazəsini al, sonra digər
   brauzerdən yeni qorunan sorğu et. Köhnə giriş qəbul edilməməlidir.
5. Normal sifariş/stok/ödəniş və bildiriş axınını təkrar yoxla. Primary DB
   bağlantılarının sayını, sorğu gecikməsini və xəta loglarını müşahidə et.
6. Redis dayanma testini yalnız ayrıca staging/test mühitində et. Baza da
   əlçatmazdırsa qorunan əməliyyatın uğurlu olmaması gözlənilir.

Commit/push və canlı server dəyişikliyi bu işin icazəsinə daxil deyil.
