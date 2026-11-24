# Log məxfiliyi və Production qorumaları

Tarix: 24.11.2026. Backend branch: `security/logging-production-hardening`.
Frontend branch: `security/redact-nginx-token-logs-ui`.

## Sadə izah

Əvvəllər server bəzi sorğuların tam ünvanını loga yazırdı. Ünvanın içində
giriş və ya parol bərpası tokeni olarsa, həmin sirr log faylına da düşürdü.
İndi əməliyyatın nəticəsini izləmək üçün lazım olan təhlükəsiz məlumat saxlanılır,
amma token, cookie, parol və sorğunun məzmunu diaqnostikaya daşınmır.

Bu dəyişiklik tokenin tətbiqə çatmasını bağlamır. Məsələn, SignalR bağlantısı
tokeni əvvəlki qaydada serverə ötürür; sadəcə onun loga yazılması dayandırılır.
İcazələr, sessiya yoxlamaları, restoran sərhədləri və biznes qaydaları dəyişməyib.

## Hazırda serverdə aktivdir

1. API `Production` rejimindədir. Açıq Swagger artıq `404` qaytarır.
2. Frontend Nginx access logu yalnız metod, status, bayt və vaxt saxlayır.
   URL, query, Referer, Authorization, Cookie, User-Agent və body yazılmır.
3. Nginx HTTP error logu söndürülüb: onun sərbəst xəta mətni tam sorğunu
   göstərə bilərdi. Əvəzində təhlükəsiz access status/vaxt diaqnostikası qalır.
   Main səviyyəsində başlanğıc və konfiqurasiya diaqnostikası saxlanılıb.
4. Caddy xəta diaqnostikasında request, uri və headers sahələri çıxarılır.
   Xəta hadisəsinin özü qalır. Paylaşılan Caddy-də digər saytların route-ları
   dəyişməyib; `Referrer-Policy: no-referrer` yalnız ECafe saytına əlavə edilib.
5. API, frontend və Caddy üçün Docker logları `10m × 3` ilə məhdudlaşdırılıb.
   Bu, limitsiz böyüməni dayandırır; bütün server diski üçün ümumi kvota deyil.
6. Sentry debug və structured log göndərişi runtime ayarlarında söndürülüb.
7. Əvvəlki loglar konteynerlər yenidən yaradılmazdan əvvəl şəxsi gzip arxivinə
   köçürülüb. Arxivlər avtomatik və ya özbaşına silinməyib.

DB, Redis və MinIO yenidən başladılmayıb. API-nin qeyri-root istifadəçisi,
Data Protection açarlarının volume-u, məxfi ayarlar və mobil APK bind mount-u
qorunub. Bu iş sessiyaları kütləvi ləğv etmir və açarları rotasiya etmir.

## Kodda hazırdır, hələ deploy edilməyib

### Təhlükəsiz exception diaqnostikası

`SafeDiagnosticException.ForLogging(error)` orijinal xətanı dəyişmədən onun
log üçün ayrıca, məhdud nüsxəsini yaradır. Növ və çağırış yeri qalır;
Message, InnerException, Data və fayl yolları loga daşınmır.

Məsələn, SMTP xətasının mətnində parol olarsa, log həmin mətni deyil,
`IOException` kimi xəta növünü saxlayır. Ödəniş kompensasiyası, retry,
transaction və istifadəçiyə verilən ümumi xəta cavabı əvvəlki qaydada işləyir.

### Sentry allowlist

`SentryPrivacy.Apply` hadisə və trace göndərişlərini açıq seçilmiş sahələrdən
yenidən qurur. Exception növü, təhlükəsiz stack frame, trace ID və zamanlar
qalır. Body, cookie, header, query, istifadəçi məlumatı, sərbəst message,
extra, breadcrumb, SQL/HTTP span təsviri çıxarılır. Request üçün yalnız
metod və origin qalır; path də göndərilmir.

Naməlum və ya emal edilə bilməyən payload göndərilmir. Bu halda biznes sorğusu
dayandırılmır. Middleware-in ayrıca göndərdiyi xəta ikinci logger hadisəsi kimi
təkrarlanmır; worker xətalarının diaqnostikası tamamilə söndürülməyib.
Əvəzində sərbəst xəta mətnindən alınan monitorinq detalı azalır.

### Outbox xəta məxfiliyi

Email və audit outbox yeni xətalarda yalnız exception növünü saxlayır.
Admin API köhnə qeydlərdəki raw `LastError` mətnini də qaytarmır: mövcud
lokallaşdırılmış ümumi mesajı göstərir. Tarixi DB qeydləri silinməyib və
uğursuz mesajların retry/processed davranışı dəyişməyib.

### Parol linkləri və frontend uyğunluğu

Yeni məktublar `/reset-password#token=...` və `/set-password#token=...`
formasındadır. `#` hissəsi HTTP ünvanı ilə serverə göndərilmir.
Frontend həm yeni fragment, həm köhnə `?token=...` linklərini qəbul edir,
tokeni router başlamazdan əvvəl yaddaşa alıb URL-dən silir. Token storage,
cookie və history state-də saxlanılmır; yalnız açıq form submit zamanı mövcud
JSON body-də API-yə göndərilir. Boş, təkrarlanan və qeyri-normal tokenlər rədd edilir.

URL təmizləndikdən sonra tam refresh edilsə, yaddaşdakı token itir: istifadəçi
məktubdakı linki yenidən açmalıdır. Köhnə query linki ilk sənəd sorğusunda
serverə çatdığı üçün ingress log qoruması ayrıca vacibdir.

Beş yeni UI mesajı backend mərkəzi kataloquna AZ/EN/RU dillərində əlavə olunub;
frontend handoff JSON-u ilə struktur müqayisəsi uyğundur.
Frontend mənbə Nginx formatı queriesiz path/status/vaxt və upstream statusunu
saxlayır, məlum tracking token path-ini gizlədir. Hazırkı runtime formatı daha
məhduddur və ümumiyyətlə path saxlamır.

## Runtime tətbiqi və geri dönüş qeydi

`deploy/security/harden_runtime.py` standartda yalnız oxunan plan göstərir.
Linux Python 3.11+, PyYAML, Docker Compose, uyğun deploy istifadəçisi və yerli
root qovluğuna giriş tələb edir. Mənbəni serverə təhlükəsiz köçürdükdən sonra:

```sh
python3 harden_runtime.py --root /opt/ecafe
# Yalnız plan və ehtiyat nüsxə qaydaları nəzərdən keçirildikdən sonra:
python3 harden_runtime.py --root /opt/ecafe --apply
```

Skript əvvəlki ayarları və logları qoruyur, native Nginx/Caddy parserini və
readiness-i yoxlayır. Yalnız API/frontend/Caddy yenidən yaradılır. Naməlum
konfiqurasiya və secret drift aşkarlandıqda manual baxış tələb edir.

İlk cəhddə public health işləsə də, daxili `localhost` yoxlaması strict Host
filterinə görə `400` aldı və geri dönüş başladı. Köhnə compose-da ayrıca
mobil override ayarları olmadığından APK/push ayarları müvəqqəti itdi.
Son ACTIVATED mobil buraxılış qeydi və APK hash/ölçü yoxlaması ilə build 15
bərpa edildi; cari API image-i köhnə versiyaya qaytarılmadı. Ayarlar əsas
compose-a daxil edildi. Readiness indi etibarlı Host istifadə edir və rollback
yalnız köhnə faylı deyil, canlı mobil ayarlarla uzlaşdırılmış baseline-ı qoruyur.
Bu hallar üçün ayrıca avtomatik testlər əlavə edilib.

Son uğurlu ehtiyat nüsxə:
`/opt/ecafe/security-backup-20261124T152802595030Z`.
Əvvəlki bərpa nüsxələri də serverdə saxlanılır. Qovluqlar `0700`, məxfi
ayar/runtime snapshot və log arxivləri `0600` rejimindədir. `api-runtime.json`
sirrlər daşıya bilər: repoya, tiketə və ya sənədə köçürülməməlidir.
Logların saxlanma müddəti və təhlükəsiz silinməsi ayrıca qərardır.

Runtime Nginx main konfiqurasiyası `/opt/ecafe/frontend-nginx-private.conf`
read-only bind mount-u ilə qorunur. Frontend deployundan sonra `nginx -T` ilə
bütün effektiv access/error sink-ləri yoxlanmalıdır. Yeni image-dəki təhlükəsiz
konfiqurasiya təsdiqlənmədən bu override çıxarılmamalıdır. Heç bir HTTP/server/
location səviyyəsində raw debug/error log əlavə edilməməlidir.
Caddy yenidən yaradılıb IP dəyişərsə API-nin etibarlı private proxy IP-si də
uzlaşdırılmalıdır; bu helper onu tətbiq zamanı yenidən oxuyur.

## Yoxlama nəticələri

- Backend tam Release test paketi: **1949 keçdi, 0 xəta, 0 skip**.
- Son Sentry duplicate-filter/type düzəlişindən sonra məqsədli Release paketi:
  **17 keçdi**. Tam paket həmin son kiçik siyasət əlavəsindən əvvəl işlədilib.
- Yekun Release build: **0 warning, 0 error**.
- Runtime transform/rollback/mobile qoruma testləri: **12 keçdi**.
- Ayrı Linux Caddy konteynerində real 502 sorğusu: xəta diaqnostikası qaldı,
  sintetik query/auth/cookie/Referer/User-Agent markerləri logda görünmədi;
  normal 200 cavabda no-referrer yoxlanıldı.
- Frontend chatının hesabatı: **525 real Nginx**, **47 parol-link browser**,
  **283 mövcud regression** yoxlaması keçdi; TypeScript/Vite build uğurludur.
  AZ/EN/RU və 320/390/1440px görünüşlər yoxlanılıb. Mövcud bundle xəbərdarlıqları qalır.
- Canlı server: readiness `200`, Swagger `404`, parol səhifəsi `200`, ECafe
  cavablarında no-referrer; sintetik token markerləri yeni frontend logunda yoxdur.
- APK build 15 konteynerdə qeyri-root tətbiq istifadəçisi ilə oxunur;
  hash/ölçü uyğundur, push ayarları saxlanılıb. Anonim mobile release cavabının
  hidden olması mövcud təhlükəsizlik qaydasıdır, xəta deyil.
- Canlı istifadəçi tokeni ilə giriş, real ödəniş, məktub və telefon push-u
  bu işdə icra edilməyib. Biznes regression-ları təcrid olunmuş test mühitindədir.

## Deploydan sonra qısa qəbul siyahısı

1. Frontend və backend uyğun dəyişikliklərini birlikdə yayımla.
2. Test hesabına parol bərpası/setup məktubu göndər: yeni `#token` linki işləsin,
   açıldıqdan sonra URL təmizlənsin və bir submit bir API sorğusu yaratsın.
3. Vaxtı keçməyən köhnə query linki də işləsin; boş/vaxtı keçmiş link rədd edilsin.
4. Adi giriş, profil keçidi və sessiya qorumalarını yoxla.
5. Müştəri → ofisiant → mətbəx statusları və bağlantı bərpasını yoxla.
6. Səlahiyyətli hesabla APK endirmə və telefona push bildirişini yoxla.
7. Yalnız sintetik markerlə log məxfiliyini yoxla; real tokeni testə və tiketə yazma.
8. Outbox retry/audit qeydi qalmalı, admin ekranında raw xəta mətni görünməməlidir.

Commit, staging və push edilməyib. Tətbiq mənbə kodu və frontend hələ canlıya
deploy edilməyib; yalnız yuxarıda ayrıca göstərilən runtime qorumaları aktivdir.

Frontend detalları: həmin branch-də `docs/token-log-hardening-ui.md`.
İstinadlar: [Docker log rotasiyası](https://docs.docker.com/engine/logging/drivers/json-file/),
[Caddy log filter](https://caddyserver.com/docs/caddyfile/directives/log#filter),
[Nginx error log](https://nginx.org/en/docs/ngx_core_module.html#error_log).
