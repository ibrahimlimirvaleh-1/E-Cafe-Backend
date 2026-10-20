# Mərkəzi tərcümə kataloqu

## İş prinsipi

- Dəstəklənən dillər `az`, `en`, `ru`; standart və ehtiyat dil `az`.
- API sorğuları `Accept-Language` başlığından dil seçir. Texniki xəta kodu, status ID-si, status/rol parser dəyərləri, route, icazə və biznes qərarı dəyişmir. Statusun görünən tərcüməsini frontend sabit dəyərdən seçir; lokallaşdırılmış etiketi biznes şərtində müqayisə etmək olmaz.
- `core.languages` AZ/EN/RU dillərini saxlayır. `core.keywords` sabit açarları, `core.keyword_translations` isə hər açarın dil üzrə qaralama mətnini saxlayır. `(KeywordId, LanguageId)` unikal açardır.
- `core.translation_catalog` aktiv yayım göstəricisi və redaktə reviziyasını saxlayır. `core.translation_releases` və `core.translation_release_entries` dəyişdirilməz yayımlardır; mətnlər JSON sütununda deyil, dil üzrə əlaqəli sətirlərdə saxlanır.
- İlk yayımadək build ilə gələn lüğət işləyir. Super administrator redaktorda ilk dəfə dərc etdikdən sonra tam lüğət DB-də saxlanır. Oxuma əməliyyatı DB-yə yazmır.
- Build kataloqu `Services/Localization/Resources` daxilində yalnız ehtiyat/ilkin mənbədir. UI-nin üç JSON lüğəti frontend kataloqu ilə eyni açarlara malik olmalıdır. Yeni mətn üçün sabit açar və üç dil birlikdə əlavə edilir; idarəetmə panelinin dəyişiklikləri əlaqəli DB cədvəllərinə yazılır.
- Yeni build açarları köhnə DB yayımına əlavə edilir. Adminin mövcud redaktələri qorunur; parametr forması dəyişmiş köhnə mətn yeni təhlükəsiz build mətni ilə əvəzlənir.
- `Version` qeyri-şəffafdır: yayım ID-si və build məzmun hash-i birlikdə köhnə browser keşini etibarsız edir. Versiyanı rəqəm kimi müqayisə etməyin.

## Keş və Nasazlıq

Backend ayrıca ölçüsü məhdud `MemoryCache` daxilində yalnız bir dəyişdirilməz üçdilli snapshot saxlayır. Soyuq paralel sorğular bir yükləmədə birləşir. TTL 30 saniyədir; yayım commit olandan sonra cari prosesin keşi dərhal silinir. Digər server instansiyaları ən gec növbəti TTL oxunuşunda yenilənir. Redis tələb olunmur.

Browser `localStorage`-da hər dil üçün lüğət və ETag saxlayır. Versiya dəyişməyibsə `304` cavabı tam lüğətin təkrar göndərilməsinin qarşısını alır. Şəbəkə və ya storage problemi zamanı browserdəki/build lüğəti işləyir. Public cavab üçün `max-age=60`, `stale-while-revalidate=3600` tətbiq edilir; CDN/proxy əlavə ediləndə həmin gecikmə də nəzərə alınmalıdır.

DB oxunuşunda bağlantı/timeout/JSON xətası zamanı public lüğət build ehtiyatına qayıdır. Adminin uğursuz yazısı uğurlu sayılmır. API-nin qalan DB asılı funksiyalarının işləməsinə bu fallback zəmanət vermir.

## API

- `GET /api/v1/localization/dictionaries/{language}`: public yayımlanmış lüğət; `If-None-Match` və `304` dəstəyi.
- `GET/PUT /api/v1/localization/preference`: yalnız aktiv istifadəçinin öz dil seçimi; sessiya versiyası dəyişmir.
- `GET/PUT /api/v1/admin/localization/draft`: platforma super administratorunun qaralaması. PUT ən çox 250 açarı bir dəfəyə dəyişir.
- `POST /api/v1/admin/localization/publish`: `{ "expectedRevision": 0 }` ilə cari qaralamanı dərc edir.
- `GET /api/v1/admin/localization/releases`: son 50 yayımın metadata-sı.
- `POST /api/v1/admin/localization/releases/{releaseId}/rollback`: əvvəlki snapshot əsasında YENİ yayım yaradır.

Admin rolu həm token, həm canlı aktiv istifadəçi qeydi ilə yoxlanılır. PG transaction kilidindən sonra rol yenidən yoxlanılır. Köhnə redaktor reviziyası `409 LocalizationDraftConflict` qaytarır. Naməlum açar, boş mətn, HTML, həddən uzun mətn və uyğunsuz parametr dəsti rədd edilir.

## Mətn və Bildiriş Sərhədləri

### Kod Məsuliyyətləri

- `Domain/Policies/ContentTranslationPolicy` sahə hədlərini, mətn təhlükəsizliyini və reviziya qaydalarını; `ContentTranslationCollectionPolicy` isə null/əvəz/silmə və AZ sinxronlaşdırılmasını idarə edir.
- `Application/Mappings` yalnız sorğu/cavab və tipli sahə xəritələrini hazırlayır; izlənən kolleksiyanın dəyişdirilməsi mapper daxilində deyil, xidmətin çağırdığı domen policy-dədir.
- `ContentTranslationAccessManager` canlı rol, sessiya, restoran təyinatı və mənbə CRUD icazəsinə cavabdehdir. Yazı transaction kilidindən sonra bu xidmət yenidən çağırılır.
- Tərcümə oxuyucusu yalnız `IContentTranslationQuery`, giriş yoxlaması ayrıca access repository, dil siyahısı isə ayrıca language repository tələb edir. Oxuma/yazma interfeysləri eyni scoped DB repository nümunəsinə bağlanır.
- Hər entity üçün ayrıca tipli persistence store və cavab profili var. Yeni tip üçün uyğun qeydiyyat/mapping əlavə olunur; ümumi saxlanma və DTO-gəzinti alqoritmlərinə yeni tip `switch`-i əlavə edilmir.

### Entity-lər Nə Saxlayır?

Hər entity öz adlı faylındadır. Əsas entity-lərin `Translations` kolleksiyası da birbaşa həmin entity faylındadır; ayrıca `TranslatableNavigations.cs` yoxdur.

| Entity | Vəzifəsi |
| --- | --- |
| `Language` | Dilin bazadakı ID-si, `az/en/ru` kodu, ana dilində adı, aktivliyi və əsas dil olub-olmaması. |
| `EntityTranslation` | Ortaq `LanguageId`, dil əlaqəsi və `Revision`. Ayrıca DB cədvəli deyil. |
| `RestaurantTranslation` | `RestaurantId` ilə restoranın görünən adını, ünvanını və filial adını dilə bağlayır. |
| `RestaurantGroupTranslation` | Qrupun görünən adını dilə bağlayır; hüquqi adı tərcümə etmir. |
| `CategoryTranslation` | Kateqoriya adını dilə bağlayır; slug sabit qalır. |
| `ItemTranslation` | Məhsulun adını, açıqlamasını və mövcud olmama səbəbini dilə bağlayır. Qiymət və stok burada deyil. |
| `TableTranslation` | Masanın görünən adını dilə bağlayır; masa nömrəsi və tutum əsas entity-də qalır. |
| `InventoryItemTranslation` | Anbar məhsulunun adını dilə bağlayır; qalıq və ölçü vahidi dəyişmir. |
| `UnitTranslation` | Ölçü vahidinin görünən adını dilə bağlayır; texniki kod və çevirmə əmsalı dəyişmir. |
| `Keyword` | `ui.common.save` kimi sabit sistem mətn açarı. Məhsul/masa adı burada saxlanmır. |
| `KeywordTranslation` | Bir açarın bir dildə redaktə edilən qaralama mətni: `KeywordId + LanguageId + Value`. |
| `TranslationCatalog` | Sistem lüğətinin tək idarəetmə qeydi: qaralama reviziyası və aktiv `PublishedReleaseId`. Mətnlərin özü deyil. |
| `TranslationRelease` | Dərc edilmiş versiyanın metadata-sı: ID, vaxt, müəllif və varsa geri qayıdışın mənbə versiyası. |
| `TranslationReleaseEntry` | Bir yayımda bir açarın bir dildəki dəyişməz mətni. Sonrakı qaralama bunu dəyişmir. |

Məsələn, `Item.Id=7`, əsas `Name="Qara çay"` olduqda tərcümə sətirləri belədir:

| ItemId | LanguageId | Name |
| --- | --- | --- |
| 7 | 1 (AZ) | Qara çay |
| 7 | 2 (EN) | Black tea |
| 7 | 3 (RU) | Чёрный чай |

Bu üç ayrı məhsul deyil. Bir məhsulun üç dil mətnidir; qiyməti, resepti, stoku və sifariş əlaqələri yenə tək əsas məhsula aiddir. `(ItemId, LanguageId)` kompozit açarı eyni dildə ikinci sətri rədd edir. Digər məzmun tərcümələri də eyni əlaqə prinsipini izləyir.

### Enum, Sabit və Seeder Fərqi

- `Domain/Enums/LanguageCode.cs`: `Az=1`, `En=2`, `Ru=3`. Kodun hər yerində mənası bilinməyən 1/2/3 əvəzinə bu seçimlər istifadə edilir. Enum adından API-nin kiçik hərfli `az/en/ru` kodu alınır; `Description` ana dildə görünən adı verir.
- `TranslationPolicy`: enum ilə dil kodu arasında çevirmə və yoxlama edir. Oxumada `en-US` kimi seçim `en` olur, tanınmayan dil AZ-yə qayıdır. Yazmada isə `1`, `EN`, `en-US` qəbul edilmir, dəqiq `en` tələb edilir. Beləliklə enum rəqəmi təsadüfən dil kodu kimi saxlanmır.
- `Infrastructure/Seeders/LanguageSeeder.cs`: enum dəyərlərindən `languages` üçün sabit ilkin ID/kod/ad/aktivlik qeydləri hazırlayır.
- `Infrastructure/Seeders/TranslationCatalogSeeder.cs`: yalnız ilkin boş kataloq qeydini yaradır: ID=1, Revision=0, aktiv yayım yoxdur.
- `LanguageConfiguration` və digər `*Configuration` faylları cədvəl, FK, indeks və ölçüləri müəyyən edir; seed məlumatı həmin fayllarda deyil. Seeder-lər `ECafeDbContext.OnModelCreating` daxilində çağırılır və EF migration modelinə daxil olur. Hər server açılışında istifadəçi redaktələrini sıfırlayan əməliyyat deyil.
- `LocalizationDefaults.CatalogId` tək kataloqun adlandırılmış ID-sidir. Bir dəyərli seçim olmadığı üçün süni enum yaradılmayıb. `TranslationLockKeys` texniki PostgreSQL kilid sahələrini adlandırır; bunlar DB-də biznes qeydiyyatı olmadığı üçün seeder-ə daşınmır.
- Mətn uzunluğu, batch limiti, reviziyanın ilkin 1 dəyəri və keş ölçüsü seçim/status deyil; uyğun sabit və qayda olaraq qalır. Hər rəqəmi enum etmək məqsəd deyil.
- `KeywordTranslation` redaktə edilən məlumatdır. Onu daimi `HasData` seed-i ilə yeniləmək admin mətnlərini sonrakı migration-da əzə bilər. Buna görə ilkin lüğət dondurulmuş migration SQL resursu ilə bir dəfə köçürülür, sonrakı dəyişikliklər admin axınından keçir. Tətbiq edilmiş migration və onun dondurulmuş resursu yenidən generasiya edilmir.

Bu struktur düzəlişi əvvəlki dil ID-lərini və API müqaviləsini dəyişmir. Yeni migrasiya yalnız EF modelində həqiqi dəyişiklik olduqda yaradılmalıdır.

### Məzmun CRUD-u Addım-Addım

1. Menecer məhsulu yaradanda AZ əsas sahələr və istəyə bağlı EN/RU `translations` siyahısı göndərilir.
2. `ItemManager` adi məhsul icazələrini və biznes qaydalarını yoxlayır. `ContentTranslationRequestMapper` sorğunu ayrıca tərcümə obyektlərinə çevirir; mətnlərin yoxlanması üçün `ContentTranslationPolicy` istifadə edilir. Mapper izlənən məhsul kolleksiyasını dəyişmir.
3. `ContentTranslationCollectionPolicy` hazır sətirləri tətbiq edir: `null` əlavə dilləri qoruyur, `[]` əlavə dillərin mətnlərini təmizləyir, dolu siyahı onları əvəz edir. Təmizlənmiş dil sətri reviziya nişanı kimi qalır; sonradan yenidən yazılanda köhnə reviziya təkrarlanmır. Əsas AZ mətnini də sinxronlaşdırır. Mövcud composite açarı yenidən yaratmır; yerində dəyişib reviziyanı artırır.
4. Məhsul və tərcümələr eyni `SaveChanges` daxilində saxlanır. Tərcümə yaratmaq qiymət/stok dəyişikliyi yaratmır.
5. Oxuma sorğusunun `Accept-Language` başlığını mövcud middleware müəyyən edir. Cavab filtrində `ContentTranslationReader` uyğun DTO sahələrini tipli profillərdən toplayır və eyni növün ID-lərini toplu sorğuda oxuyur. Hər məhsul üçün ayrıca DB sorğusu açılmır.
6. EN mətni varsa EN görünür; yoxdursa əsas AZ sahəsi qalır. Sifariş snapshot-u, şəxsi ad və sərbəst qeyd profilə daxil edilmir.
7. Mənbə soft-delete olduqda əlaqəli tərcümələr filter ilə gizlənir; fiziki silmədə məzmun tərcümələri FK cascade qaydasını izləyir. Yalnız məhsulu deaktiv etmək tərcüməni silmir.

Mövcud tərcüməni ayrıca redaktə edəndə `ContentTranslationManager` əvvəl `ContentTranslationAccessManager` vasitəsilə canlı istifadəçi/rol/sessiya/restoran/icazə yoxlaması edir. Transaction və entity kilidindən sonra bunu yenidən yoxlayır. `expectedRevision` uyğun gəlirsə uyğun tipli store yazır/təmizləyir, uyğun deyilsə 409 alınır. `ContentTranslationRepository` store seçir; hər store yalnız öz entity-sinin SQL proyeksiyası və saxlanmasına cavabdehdir. Field-copy işi `Application/Mappings` daxilində qalır.

Silinib yenidən yaradılan tərcümənin reviziyası 1-ə qayıtmır: məsələn, ilk mətn 1, silinmə 2, yeni mətn 3 olur. Köhnə ekrandan `expectedRevision=1` ilə PUT və DELETE yeni mətni dəyişmir. API silinmədən sonra mətnləri null, cari reviziyanı isə qorunan şəkildə qaytarır; UI yenidən oxuduğu həmin reviziyanı göndərməlidir. Əsas entity fiziki silinəndə FK cascade həmin nişan sətirlərini də silir.

Adi mənbə CRUD-u yeniləmədən əvvəl repository vasitəsilə bütün əlaqəli dil sətirlərini yükləyir, o cümlədən müvəqqəti deaktiv dili. Yalnız yoxlanmış owner-in translation kolleksiyası üçün query filter buraxılır; əsas entity-nin soft-delete və restoran filtrləri qalır. Beləliklə deaktiv dil yenidən aktivləşəndə köhnə tərcümə geri çıxmır və təkrar composite açar yaranmır. İctimai oxuma deaktiv dilləri göstərmir. CRUD-un saxlanmasında yalnız tərcümə sətirinə aid paralel reviziya/təkrar açar konflikti 409-a çevrilir; başqa DB xətaları gizlədilmir.

Məhsul və restoran tərcümələri üçün ayrıca “Dərc et” yoxdur: uğurlu saxlamadan sonra növbəti uyğun API oxunuşunda görünür. Aşağıdakı yayım axını yalnız sistem lüğətinə aiddir.

### Sistem Lüğətinin Axını

1. Admin `ui.common.save` açarının AZ/EN/RU mətnini dəyişib saxlayır. `TranslationManager` placeholder və təhlükəsizlik yoxlamasından sonra `KeywordTranslation` qaralamasını yeniləyir, kataloq reviziyasını artırır. İstifadəçilər hələ əvvəlki dərc edilmiş mətni görür.
2. “Dərc et” zamanı yeni `TranslationRelease` və bütün dil/açar mətnləri üçün `TranslationReleaseEntry` sətirləri yaranır. `TranslationCatalog.PublishedReleaseId` bu yeni versiyanı göstərir. Bunlar eyni transaction-da commit edilir.
3. Backendin cari lüğət keşi etibarsız edilir. Public oxuma aktiv yayımın mətnlərini və versiyasını qaytarır; ETag eynidirsə browserə 304 verilə bilər. Keş və browser detalları yuxarıdakı “Keş və Nasazlıq” hissəsindədir.
4. Geri qayıtma əvvəlki yayımın sətirlərini dəyişmir. Həmin mətnlərlə yeni versiya yaradır və `SourceReleaseId` əvvəlki versiyanı göstərir.
5. İlk dərcədək və ya lüğət DB oxunuşunda uyğun nasazlıq olduqda `TranslationBootstrap` build ilə gələn təhlükəsiz ehtiyatı verir. Bu JSON faylları əsas redaktə DB-si deyil. Public lüğət oxuma DB-yə yazmır.

Məsələn: aktiv yayımda AZ “Saxla” var. Qaralamada “Yadda saxla” edib yalnız saxlasan, istifadəçi “Saxla” görür. Dərc edəndə yeni versiya aktiv olur və lüğətin növbəti yenilənməsində “Yadda saxla” görünür. `ItemTranslation` içində “Black tea” redaktəsi isə bu yayım göstəricisindən asılı deyil.

Restoran, qrup, kateqoriya, məhsul, xüsusi masa adı, anbar məhsulu və ölçü vahidi üçün ayrıca `*Translation` entity-ləri və real xarici açarlar var. Yalnız həmin entity-lərin göstərilən mətnləri seçilmiş dildə oxunur. Müştəri/işçi adları, hüquqi adlar, sərbəst qeydlər, ləğv/etiraz səbəbləri, sifarişin ad/qiymət snapshot-ları və yüklənmiş sənədlər avtomatik tərcümə edilmir. PDF müqavilə şablonu və əvvəl göndərilmiş məktub ayrıca sənəddir; tarixçə yenidən yazılmır.

### Entity CRUD Müqaviləsi

- Mövcud create/update mətn sahələri AZ əsas mənbə kimi qalır. Optional `translations` yalnız EN/RU sətirlərini qəbul edir: `{ "languageCode": "en", "name": "Tea", "description": "Black tea" }`. `description` və `unavailableReason` məhsula; `location` və `branchName` restorana aiddir.
- `translations` göndərilmirsə və ya `null` olarsa əlavə dillər qorunur. `[]` EN/RU mətnlərini təmizləyir, reviziya nişanları isə qalır. Siyahı göndərilərsə əlavə dillərin tam yeni siyahısıdır; qorunacaq dillər də daxil edilməlidir. AZ bu siyahıda qəbul edilmir, əsas sahələrdən eyni SaveChanges əməliyyatında sinxronlaşdırılır.
- Multipart sorğularda indeksli sahələr göndərilir: `Translations[0].LanguageCode=en`, `Translations[0].Name=Tea`. Məhsul yeniləməsində əlavə dilləri tam silmək üçün `ClearTranslations=true` istifadə edilir; bu bayraqla dolu `Translations` siyahısını eyni sorğuda göndərmək rədd olunur. Məhsul formunda hər ikisini göndərməmək mövcud tərcümələri qoruyur. Restoran qeydiyyatında boş siyahını göndərməmək kifayətdir, restoran yeniləməsi JSON qəbul edir.
- Boş/çatışmayan tərcümə sahəsi oxuma zamanı əsas mətnə qayıdır. Tərcümə yoxdursa avtomatik maşın tərcüməsi yaradılmır. Sərbəst qeydlər tərcümə kimi qəbul edilmir.
- Mənbə soft-delete olduqda tərcümə sətirləri query filter ilə görünmür; fiziki silmədə əlaqə sətirləri cascade ilə silinir. Deaktivləşdirmə tərcümələri itirmir. Masa surəti özünə aid yeni tərcümə sətirləri yaradır.
- Siyahı cavablarında mətnlər entity növünə görə toplu oxunur. Restoran/menyu axtarışı mənbə və seçilmiş dil üzrə SQL-də edilir; səhifələmədən sonra yaddaşda axtarış aparılmır.

### Tərcümə Redaktoru API

- `GET /api/v1/localization/languages`: aktiv dəstəklənən dillər.
- `GET /api/v1/content/{entityType}/{id}/translations`: xam AZ əsas sahələri və aktiv dillərin fallback edilməmiş mətnləri. Cavab: `{ entityType, id, restaurantId, translations: [{ languageCode, revision, name, description, location, branchName, unavailableReason }] }`.
- `PUT /api/v1/content/{entityType}/{id}/translations/{languageCode}`: yalnız EN/RU. `expectedRevision` məcburidir: yeni sətir üçün 0, yeniləmədə GET-dən gələn rəqəm. Köhnə reviziya 409 qaytarır.
- `DELETE /api/v1/content/{entityType}/{id}/translations/{languageCode}?expectedRevision=...`: yalnız seçilmiş EN/RU mətnlərini təmizləyib reviziyanı artırır; mənbə obyekt və maliyyə əlaqələri dəyişmir. AZ silinə bilməz.
- `entityType`: `restaurants`, `restaurant-groups`, `categories`, `items`, `tables`, `inventory-items`, `units`. Restoran məzmunu yalnız aid restoranın səlahiyyətli meneceri/sahibkarına açıqdır; qrup və ölçü vahidi redaktoru yalnız platforma super administratoruna açıqdır.
- Redaktə forması göstərilən lokallaşdırılmış DTO-dan AZ dəyərini götürməməlidir: xam mətn bu redaktor GET-dən alınır. Yükləmə və ya giriş xətasında tərcümələr boş siyahı kimi saxlanılmamalıdır.

Geri ödəniş cavabında `status` və `statusId` dəyişmir; göstərilən tərcümə `statusLabel` sahəsində gəlir. Tarixçədə `fromStatusId`, `toStatusId`, `fromStatusLabel`, `toStatusLabel` əlavə olunur. `reason` orijinal qalır; yalnız məlum provayder mərhələsinin serverdə yaradılmış səbəbi `reasonLabel` ilə tərcümə edilir. Naməlum/sərbəst səbəb üçün `reasonLabel=null` olduqda orijinal göstərilməlidir. `customerNextStep` və `eligibilityReason` yalnız kataloqda tanınan sistem mətnləri üzrə çevrilir; `cancellationReason` dəyişmir. Dil seçimi dəyişəndə frontend API-dən həmin dilin cavabını yenidən almalıdır; maliyyə əməliyyatını təkrarlamamalıdır. Bu görünüş sahələri üçün migration tələb olunmur.

Kodlu API xətaları kataloqdan gəlir. Köhnə sabit sistem mesajları tam uyğun build aliasları ilə tanınır; mətnin fraqmentləri və istifadəçi məzmunu sərbəst tərcümə edilmir. Naməlum əvvəlki mesajın orijinalı saxlanır. Yeni bildirişlərdə mümkün olduqca sabit açar/parametr kataloq sətri istifadə edilməlidir.

Yeni tanınan sistem bildirişi alıcının saxlanmış dilində hazırlanır; e-mail outbox-a bu mətn düşür. Bildiriş açarları və parametrləri saxlanır ki, ekranda sonradan dil dəyişmək mümkün olsun. Əvvəlki açarsız bildirişlər olduğu kimi qalır. Mobil kilid ekranında şəxsi sifariş/depozit məlumatı deyil, alıcının dilində ümumi yeni bildiriş mətni göstərilir. Push icazəsi, restoran scope-u və aktiv cihaz qaydaları dəyişmir. Tarixçədəki texniki status kodları sabitdir.

## Deploy

Migration-lar: əvvəl `20261018105154_AddTranslationCatalog`, sonra `20261019190941_RelationalMultilingualContent`. Sonuncu migration dondurulmuş 2120 açar/6360 dil sətrini əlavə edir, əvvəlki JSON redaktələri ilə onları əvəzləyir, bütün əvvəlki yayımları əlaqəli sətirlərə köçürür və yalnız bundan sonra köhnə JSON sütunlarını silir. Mövcud entity mətnləri AZ əlaqə sətirlərinə köçürülür; ilkin biznes məlumatları dəyişmir.

Əvvəl ehtiyat nüsxəsi və staging yoxlaması aparılmalıdır. Server bazasına migration tətbiq edilməyib. Bu keçid zamanı köhnə API prosesləri dayandırılmalıdır: köhnə kod artıq silinən JSON sütunlarını oxuyur, ona görə qarışıq köhnə/yeni instansiyalarla rolling deploy uyğun deyil. Downgrade sistem lüğətini JSON-a bərpa edir, amma EN/RU biznes tərcümələri varsa onların itirilməməsi üçün əməliyyatı dayandırır; əvvəl export/backup və ayrıca geri keçid planı lazımdır.

```powershell
dotnet ef database update --project src/ECafe.Infrastructure --startup-project src/ECafe.Api --context ECafeDbContext
```

Bu əmr yalnız seçilmiş mühitin mövcud təhlükəsiz connection konfiqurasiyası ilə işlədilməlidir; connection string və secret-i terminal tarixçəsinə yazmayın. Migration istifadəçiyə `preferred_language=az`, əvvəlki bildirişlərə `Language=az` verir, bildirişin orijinal mətnini və biznes məlumatlarını dəyişmir.

## Əl İlə Yoxlama

1. Təmiz browserdə AZ açılsın; EN və RU seçimindən sonra refresh-də seçilmiş dil qalsın.
2. Login/logout, müştəri, menecer, ofisiant və mətbəx ekranlarında dili dəyişin. Başlıq, düymə, tarix/say və modal mətnlərini yoxlayın.
3. Tərcüməsi olmayan restoran, masa və məhsul adlarının əsas dildə qaldığını, tərcüməsi olanların seçilmiş dildə göründüyünü yoxlayın. Müştəri adları və əvvəlki sifariş snapshot-ları dəyişməməlidir.
4. Dil seçimini hesabda saxlayın; başqa browserdə həmin hesabla girişdə seçim bərpa olunsun.
5. Eyni uğursuz əməliyyatı üç dildə göndərin: xəta kodu/status eyni, mesaj seçilmiş dildə olsun.
6. Eyni lüğət sorğusunu ETag ilə təkrarlayın: `304`; başqa dil üçün fərqli ETag.
7. Super administrator bir mətni dəyişib yalnız saxlasın: public ekranda əvvəlki yayımlanmış mətn qalsın.
8. Dərc etsin: public lüğətin versiyası dəyişsin və browserin növbəti yeniləməsində yeni mətn görünsün.
9. Köhnə reviziya ilə ikinci tabdan saxlama/yayım `409` qaytarsın; ilk tabın işi itirilməsin.
10. Menecer/müştəri hesabı admin endpointlərini birbaşa çağırdıqda `403` alsın.
11. HTML və silinmiş/dəyişdirilmiş `{parametr}` ilə saxlama rədd edilsin.
12. Əvvəlki yayıma qayıdın: yeni release ID-si yaransın, əvvəlki tarixçə dəyişməsin.
13. Alıcının dili RU, əməliyyatçının dili EN olduqda yeni sistem bildirişi/e-mail RU, mobil ümumi push RU olsun.
14. Şəbəkəni bağlayıb əvvəl yüklənmiş dilə keçin: UI boş qalmasın; reconnect-dən sonra versiya yenidən yoxlanılsın.
15. Rezervasiya-depozit-sifariş-mətbəx-təqdim-hesablaşma-sessiya bağlama və refund axınlarını yenidən keçin: məbləğ, icazə, status keçidi və idempotentlik dəyişməməlidir.
16. Hər məzmun növündə EN/RU tərcüməsi yaradın, yeniləyin və silin; AZ əsas məlumatı, ID-lər və digər dillər ayrıca redaktə zamanı dəyişməməlidir. Əsas CRUD-da `null`/göndərilməyən siyahı tərcümələri qorumalı, `[]` isə əlavə dilləri təmizləməlidir.
17. Başqa restoran meneceri və əsas yazı icazəsi ləğv edilmiş istifadəçi tərcümə endpointlərinə daxil ola bilməməlidir. Köhnə `expectedRevision` ilə ayrıca redaktə 409 verməlidir.
18. Mənbə məlumatını silin: tərcümələr public siyahı və redaktorda görünməməlidir. Məhsulu yalnız deaktiv etdikdə tərcümələri itirilməməlidir.
