# Təkrarlanan private metodların refaktoru

Branch: `security/logging-production-hardening`. Əvvəlki təhlükəsizlik
dəyişiklikləri qorunub. Commit, push, frontend və canlı server dəyişikliyi yoxdur.

## Nə birləşdirildi

17 istifadəçi faylında 26 private metod nüsxəsi çıxarılıb, 8 məqsədli helper
yaradılıb. Ortaq `Helpers` qovluğuna əlaqəsiz metodlar yığılmayıb: hər helper
uyğun Application məsuliyyətinin yanında saxlanılıb.

| Helper | Əvvəl təkrarlanan məntiq | İstifadəçilər |
| --- | --- | --- |
| `Common/Security/AuthenticationTokenHelper` | URL-safe token, SHA-256 hash, ilkin məlum olmayan parol hash-i | Auth, User, Restaurant, PasswordReset, PasswordSetup, EmailChange |
| `Common/Configuration/ConfigurationValueReader` | Müsbət tam ayar və fallback oxunması | LoginAttempt, EmailOutbox, OutboxAdmin |
| `Common/Validation/RestaurantValidation` | Təkrarlanan həftə günləri və saat qurşağı | RegisterRestaurant, UpdateRestaurant validatorları |
| `Common/Validation/ItemStatusValidation` | Məhsul status tipinə aid ID yoxlaması | CreateItem, UpdateItem validatorları |
| `Services/FileValidation/FileUploadPolicyFactory` | Fayl tipindən extension/MIME/ölçü limitlərinin köçürülməsi | FileUpload handler, ContractFileService |
| `Services/Auth/PasswordLinkBuilder` | Frontend ünvanı/fallback və fragment token linki | PasswordReset, PasswordSetup |
| `Services/Auth/UserRegistrationValidation` | Normalizasiya edilmiş email/telefonun repository ilə unikallıq yoxlaması | AuthManager, UserManager |
| `Features/Commands/File/UnattachedFileCompensation` | Uğursuz upload-dan sonra təhlükəsiz best-effort cleanup | SubmitPaymentProof, SubmitRefundTransfer handler-ləri |

## Nələr dəyişmədi

- Parol tokeni əvvəlki kimi 64 təsadüfi baytdır, URL-safe Base64 formatındadır.
- SHA-256 UTF-8 və böyük hərfli hex formatı saxlanıb: mövcud token hash-ləri
  yeni helper ilə də eyni nəticə verir. Email dəyişmə tokeninin ayrıca 32 baytlıq
  hex formatı dəyişməyib; yalnız onun hash hesablaması ortaqdır.
- İlkin parol 32 təsadüfi baytın BCrypt hash-idir. Parol qüvvəsi və token TTL-i dəyişməyib.
- Yeni parol linkləri əvvəlki təhlükəsizlik işindəki `#token=` qaydasını saxlayır.
  Email dəyişmə linkinin daha sərt HTTPS yoxlaması bu helper-ə qatılmayıb.
- Konfiqurasiyada səhv/sıfır/mənfi dəyər həmin çağıranın əvvəlki fallback-inə qayıdır.
- Validation şərtləri, mesajları və status tipi sərhədləri saxlanıb.
- Unikallıq yoxlamasında email xətası telefon xətasından əvvəl qaytarılır;
  sorğuların sırası, repository filtrləri və DB məhdudiyyətləri dəyişməyib.
- Cleanup əvvəlki kimi sorğunun cancellation tokenindən asılı deyil, uğursuzluğu
  əsas biznes xətasını əvəz etmir. Əvvəlki təhlükəsiz exception logu saxlanıb.
- İcazə, sessiya, restoran sərhədi, SQL kilidi, transaction və cache-invalidation
  axınları dəyişməyib. Schema, paket və DI qeydiyyatı dəyişikliyi lazım olmayıb.

## Niyə hər oxşar ad birləşdirilmədi

`RequireSessionAsync`, `GetAttachableFileAsync`, `GetUnitAsync`,
`EnsureRestaurantExistsAsync` və IP oxuma metodlarını yalnız adına görə
eyniləşdirmək olmaz. Məsələn, bəzisi əlavə tenant/rol/ID yoxlaması edir,
bəzisi ayrı repository yoluna gedir, fərqli xəta qaytarır və ya fərqli header
fallback-i saxlayır. Bu sərhədləri parametr və callback-lə dolu bir universal
helper-ə yığmaq təhlükəsizlik qaydalarını gizlədərdi.

Eyni səbəbdən UTC etiketini qoyan metodla local vaxtı UTC-yə çevirən metod
birləşdirilməyib. Sadə birsətirlik və yalnız öz axınına aid metodlar da
məqsədsiz abstraksiyaya çevrilməyib.

## Testlər

`PrivateHelperRefactoringTests` 44 yeni sınaqla token formatını və uyğun hash-i,
konfiqurasiya sərhədlərini, hər iki validatorun davranışını, parol linklərini,
upload siyasətinin müstəqil nüsxəsini, unikallığın xəta prioritetini və cleanup
xətasının təhlükəsiz işlənməsini yoxlayır. Əvvəlki 17 log məxfiliyi testi ilə
birlikdə ilkin məqsədli paketdə 61/61 keçib.

İlk geniş test cəhdində yalnız test bağlantısı yanlış başlanğıc DB adı ilə
verilmişdi: qoruyucular `postgres` tələb edir. Onlar zəiflədilməyib; ayrıca
loopback klasterinin test bağlantısı düzəldilərək tam paket yenidən işlədilib.

Yekun Release regression paketi: **1993/1993 keçdi, 0 xəta, 0 skip** (6 dəqiqə
46 saniyə). Buna 44 yeni helper testi, əvvəlki 17 log məxfiliyi testi, həqiqi
PostgreSQL paralellik/rollback və ayrıca ACL-li Redis bərpa sınaqları daxildir.
Əvvəlki runtime təhlükəsizlik transformlarının 12 Python testi də yenidən keçib.

Yekun TRX: workspace-in repodan kənar `tmp/helper-refactor-20261124/helper-full-final.trx`
faylındadır; ilkin səhv bağlantı cəhdi ayrıca `helper-full.trx` kimi saxlanılıb.
Heç bir test qoruyucusu söndürülməyib və canlı serverə müraciət edilməyib.
