namespace ECafe.Api.Swagger;

public static class EcafeSwaggerMetadata
{
    public static readonly IReadOnlyDictionary<string, SwaggerTagInfo> Tags =
        new Dictionary<string, SwaggerTagInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth"] = new("01. Authentication", "Login, qeydiyyat və refresh token əməliyyatları."),
            ["RestaurantSchedule"] = new("02.1. Restaurant Schedule", "İş saatı dəyişikliyi və müştəriyə verilən təklif."),
            ["RestaurantScheduleFlow"] = new("02.2. Restaurant Schedule Flow", "İş saatı təklifi, razılıq, tətbiq və geri götürmə."),
            ["Restaurant"] = new("02. Restaurants", "Restoran kataloqu, restoran profili və public booking üçün əsas məlumatlar."),
            ["Category"] = new("03. Menu Categories", "Restoran menyu kateqoriyalarının idarə olunması."),
            ["Item"] = new("04. Menu Items", "Menyu məhsulları, qiymət, status və şəkil məlumatları."),
            ["Table"] = new("05. Tables", "Restoran stollarının yaradılması və gələcək availability axınının bazası."),
            ["TableSession"] = new("05.1. Table Sessions", "Ofisiantın özünə bağlı açıq masa sessiyalarının xidmət görünüşü."),
            ["TableSessionFlow"] = new("05.3. Table Session Flow", "İşgünü bitmiş açıq sessiyanın menecer tərəfindən yoxlanması və xidmətin davamına qərar verilməsi."),
            ["Order"] = new("05.2. Orders", "Ofisiantın öz açıq masa sessiyası üçün sifariş yaratması və oxuması. Mətbəxə göndərmə bu mərhələyə daxil deyil."),
            ["OrderFlow"] = new("05.4. Order Flow", "Ofisiantın mövcud sifarişini mətbəx növbəsinə təkrara davamlı göndərməsi."),
            ["Kitchen"] = new("05.5. Kitchen", "Yalnız aktiv mətbəx işçisi üçün real, səhifələnmiş sifariş növbəsi."),
            ["KitchenFlow"] = new("05.6. Kitchen Flow", "Mətbəxdə qəbul, hazırlama və hazır mərhələlərinin təhlükəsiz və təkrara davamlı idarəsi."),
            ["User"] = new("06. Users & Staff", "İstifadəçi, staff, ofisiant və profil əməliyyatları."),
            ["File"] = new("07. Files", "Şəkil və fayl yükləmə/göstərmə endpoint-ləri."),
            ["Reservation"] = new("08. Reservations", "Müştərinin rezervasiya yaratması, siyahısı, detalı və tarixçəsi."),
            ["RestaurantReservation"] = new("09. Restaurant Reservations", "Restoran üzrə rezervasiya siyahısı, detalı və tarixçəsi."),
            ["ReservationFlow"] = new("10. Reservation Flow", "Rezervasiyanın ödəniş, ləğv, gəliş və tamamlanma əməliyyatları."),
            ["ReservationArrival"] = new("11. Reservation Arrival", "Gecikmə seçimləri və mövcud təklifin məlumatları."),
            ["ReservationArrivalFlow"] = new("11.1. Reservation Arrival Flow", "Gecikmə təklifinin yaradılması və müştərinin razılığı."),
            ["ReservationRefund"] = new("12. Reservation Refunds", "Geri ödənişin detalı, tarixçəsi və icazəli rekvizit baxışı."),
            ["ReservationRefundFlow"] = new("13. Reservation Refund Flow", "Geri ödəniş sorğusu, rekvizit, köçürmə, təsdiq və etiraz əməliyyatları."),
            ["RestaurantContract"] = new("14. Restaurant Contracts", "Restoran müqavilələrinin yaradılması, yenilənməsi və məlumatları."),
            ["RestaurantContractFlow"] = new("15. Restaurant Contract Flow", "Müqavilənin təsdiqə göndərilməsi, sahibkar təsdiqi, aktivləşdirilməsi və ləğvi."),
            ["MobileApp"] = new("16. Mobile App", "Mobil tətbiqin restoran üzrə ayarları, yükləmə linkinin yayımlanması və push cihaz qeydiyyatı.")
        };

    public static readonly IReadOnlyDictionary<string, SwaggerEndpointInfo> Endpoints =
        new Dictionary<string, SwaggerEndpointInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth.Login"] = new("İstifadəçi girişi", "Email/telefon və şifrə ilə daxil olur, JWT access token və refresh token qaytarır."),
            ["Auth.Register"] = new("Müştəri qeydiyyatı", "Yeni müştəri hesabı yaradır. Restoran owner/staff hesabları üçün staff yaratma endpoint-i istifadə olunur."),
            ["Auth.Refresh"] = new("Access token yenilə", "Refresh token əsasında yeni access token alır."),
            ["Restaurant.RegisterRestaurant"] = new("Restoran yarat", "Admin/owner restoran profilini yaradır. Depozit məbləği, ləğv pəncərəsi və xidmət haqqı faizi restoran səviyyəsində saxlanılır."),
            ["Restaurant.GetAllRestaurants"] = new("Aktiv restoranları gətir", "Müştəri saytında görünəcək aktiv restoran kataloqunu qaytarır: ad, ünvan, əlaqə, reytinq, şəkillər və restoran booking ayarları."),
            ["Restaurant.GetByIdRestaurant"] = new("Restoran detalını gətir", "Seçilmiş restoranın profilini, stollarını, menyu kateqoriyalarını və məhsullarını qaytarır."),
            ["Category.GetAll"] = new("Menyu kateqoriyalarını gətir", "Restorana aid menyu kateqoriyalarını qaytarır."),
            ["Category.Create"] = new("Menyu kateqoriyası yarat", "Restoran menyusu üçün yeni kateqoriya yaradır."),
            ["Item.Create"] = new("Menyu məhsulu yarat", "Restoran menyusuna məhsul əlavə edir. Şəkil optional ola bilər."),
            ["Item.Update"] = new("Menyu məhsulunu yenilə", "Restoran menyusundakı məhsulun kateqoriya, status, qiymət və şəkil məlumatlarını yeniləyir."),
            ["Item.Deactivate"] = new("Menyu məhsulunu deaktiv et", "Menyu məhsulunu satışdan və aktiv siyahıdan çıxarır."),
            ["Item.Delete"] = new("Menyu məhsulunu sil", "Menyu məhsulunu soft-delete edir və aktiv siyahıdan çıxarır."),
            ["Item.GetAll"] = new("Menyu məhsullarını gətir", "Menyu məhsullarını səhifələmə, kateqoriya və status filterləri ilə qaytarır."),
            ["Table.CreateTable"] = new("Stol yarat", "Restoran üçün stol nömrəsi, ad, tutum və aktivlik məlumatı yaradır."),
            ["TableSession.GetCloseReviews"] = new("Bağlanması yoxlanmalı masaları gətir", "Yalnız restoranın aktiv meneceri/sahibkarı üçün. Tarix filtrindən asılı olmayan səhifələnmiş siyahı. İş saatı, gecəyarısından sonrakı bağlanma və növbəti baxış vaxtı serverdə hesablanır. Status dəyişmir; sessiya avtomatik bağlanmır."),
            ["TableSessionFlow.ContinueService"] = new("Masa xidmətinin davam etdiyini təsdiqlə", "Aktiv menecer/sahibkar üçün. Body: reviewAgainAt (offset ilə gələcək tarix, standart maksimum 24 saat), reason (məcburi, maksimum 500 simvol). Yalnız yoxlama tələb edən açıq sessiya üçün; qərar tarixçə və auditdə saxlanır. Çıxış vaxtı, sifariş və ödəniş dəyişmir. Növbəti baxış vaxtında yenidən tək bildiriş dövrü olur."),
            ["ReservationFlow.CompleteOwnService"] = new("Ofisiantın öz masa xidmətini bitir", "Yalnız aktiv ofisiantın özünə bağlı sessiyası üçün. Müştərinin ayrıldığı təsdiqləndikdən sonra çağırılır. Masa kilidi altında sessiya bağlanır və rezervasiya tamamlanır; sifariş/ödəniş/refund dəyişmir. Təkrar uğurlu çağırış ikinci tarixçə və bildiriş yaratmır. Menecer əvvəlki complete endpointindən istifadə edir."),
            ["Order.Create"] = new("Masa sessiyası üçün sifariş yarat", "Yalnız aktiv ofisiantın öz açıq sessiyası və aktiv restoran müqaviləsi üçün. Idempotency-Key headerində yeni UUID göndərin; eyni sorğunu eyni açarla təkrarlamaq ikinci sifariş yaratmır (200), ilk yaradılma 201 qaytarır. Eyni açar və fərqli məzmun 409 qaytarır. Body: note, items[{itemId,quantity,note,expectedUnitPrice?}]. Qiymət, masa, restoran, müştəri, ofisiant, mənbə və status serverdə müəyyənləşdirilir. ExpectedUnitPrice yalnız qiymət dəyişikliyinin yoxlanmasıdır, qiymət mənbəyi deyil. Subtotal yalnız məhsulların cəmidir; depozit, xidmət haqqı və ödəniş hesablanmır. Created statusunda saxlanır, mətbəxə göndərilmir və stokdan silinmir. Təkrar çağırış üçün də sessiya açıq qalmalıdır. Limitlər Orders:Creation konfiqurasiyasındadır."),
            ["Order.GetForSession"] = new("Masa sessiyasının sifarişlərini gətir", "Yalnız cari ofisiantın öz açıq sessiyası üçün səhifələnmiş siyahı. PageNumber=1, PageSize=20, maksimum 100. Ən yeni sifariş əvvəldədir; saxlanılmış ad və qiymətlər qaytarılır, cari menyu ilə əvəz edilmir."),
            ["Order.GetById"] = new("Masa sessiyasının sifariş detalı", "Yalnız cari ofisiantın öz açıq sessiyasına aid sifariş üçün. Başqa sessiyanın və ya mövcud olmayan sifarişin ID-si 404 qaytarır. Məhsul sətirləri, saxlanılmış qiymətlər, mənbə, status və məhsul cəmi göstərilir; maliyyə rekvizitləri qaytarılmır."),
            ["OrderFlow.SendToKitchen"] = new("Sifarişi mətbəxə göndər", "POST, body yoxdur. Yalnız aktiv ofisiantın öz açıq sessiyasında yaratdığı Created sifariş üçün. SentToKitchenAt doldurulur, Created statusu qəbul gözlədiyini bildirir. Eyni orderId üzrə təkrar çağırış əlavə bildiriş və audit yaratmır; yeni Idempotency-Key lazım deyil. Başqa sifariş 404, göndərilməsi mümkün olmayan status 409. Aktiv mətbəx işçilərinə mövcud notification/outbox vasitəsilə xəbər verilir. Stok, ödəniş, qiymətlər və rezervasiya dəyişmir."),
            ["Kitchen.GetOrders"] = new("Mətbəxin real sifariş növbəsini gətir", "Yalnız həmin restoranın bazada aktiv Kitchen rolu və ManageKitchenOrders icazəsi olan işçisi üçün. PageNumber=1, PageSize=20, maksimum 100. Yalnız SentToKitchenAt dolu Created/Accepted/Preparing/Ready sifarişlər; ən əvvəl göndərilən əvvəl göstərilir. Göndərilməmiş, Scheduled, Served, Closed və Cancelled daxil deyil. Tarix və sessiya bağlanma filtri yoxdur; gecikmiş göndəriş gizlənmir. Məhsulun saxlanılmış adı, sayı və qeydləri qaytarılır; qiymət, müştəri və ödəniş məlumatı çıxarılmır. Qəbul/hazırlama əməliyyatı etmir."),
            ["KitchenFlow.Accept"] = new("Mətbəxdə sifarişi qəbul et", "Body yoxdur; aktiv Kitchen rolu və restoran aidiyyəti bazada yoxlanılır. Yalnız göndərilmiş Created sifariş Accepted olur. AcceptedAt və AcceptedByUserId ilk qərarı saxlayır. Eyni addımı ilk əməkdaşın təkrarı 200, başqa əməkdaşın təkrarı 409 qaytarır. Bir əməkdaş bir neçə sifarişi qəbul edə bilər. Yeni göndəriş deyil, ona görə mövcud işin işlənməsi müqavilə bitməsi ilə dayandırılmır."),
            ["KitchenFlow.StartPreparing"] = new("Sifarişin hazırlanmasına başla", "Body yoxdur; yalnız Accepted → Preparing. Ümumi növbədə başqa aktiv mətbəx əməkdaşı da işi davam etdirə bilər. PreparingAt və PreparingByUserId saxlanır; eyni qərarın təkrarı vaxtı/auditi dəyişmir. Mərhələ atlamaq və statusu geriyə qaytarmaq olmaz. Qiymət, stok və ödəniş dəyişmir."),
            ["KitchenFlow.MarkReady"] = new("Sifarişi hazır qeyd et", "Body yoxdur; yalnız Preparing → Ready. ReadyAt, ReadyByUserId, audit və məsul aktiv ofisianta OrderReady bildirişi eyni transaction-dadır. Təkrar əməliyyat ikinci bildiriş yaratmır. KitchenOrderResponse qiymətsiz görünüşü, status vaxtlarını və CanAccept/CanStartPreparing/CanMarkReady növbəti addım işarələrini qaytarır. Hazır sifariş mətbəx növbəsində qalır; servis, ödəniş və sessiya bağlanması bu endpointin işi deyil."),
            ["TableSession.GetMyOpenSessions"] = new("Xidmət etdiyim açıq masaları gətir", "Yalnız seçilmiş restoranda bazada aktiv ofisiant təyinatı olan istifadəçi üçün. Özünə bağlı açıq sessiyaları açılma vaxtı, sonra ID üzrə artan sıra ilə qaytarır. PageNumber=1, PageSize=20; maksimum 100. Ofisiant ID-si sorğudan alınmır. Başqa ofisiantın, bağlı, ləğv edilmiş və silinmiş sessiyaları göstərilmir. Tarix filtri yoxdur: köhnə, amma bağlanmamış sessiya da görünür. Maliyyə və şəxsi əlaqə məlumatları qaytarılmır; tarixlər UTC, TimeZone restoranın saat zonasıdır."),
            ["TableSession.GetMyOpenSession"] = new("Xidmət etdiyim açıq masanın detalı", "Yalnız cari ofisiantın seçilmiş restoranda özünə bağlı açıq sessiyası üçün. Mövcud olmayan, başqa ofisianta/restorana aid və ya bağlı sessiya eyni 404 cavabı alır. Aktiv ofisiant təyinatı olmayan hesab 403 alır. Masa, müştərinin adı, varsa rezervasiya və çıxış vaxtı göstərilir; sifariş yaratmır, masa sessiyası açmır və maliyyə məlumatları qaytarmır."),
            ["User.Create"] = new("Staff istifadəçisi yarat", "Owner/manager/ofisiant kimi restoran staff hesabı yaradır və restorana bağlayır."),
            ["User.Delete"] = new("İstifadəçini sil", "Staff istifadəçisini soft-delete edir."),
            ["User.UpdateRole"] = new("İşçinin rolunu dəyiş", "Seçilmiş restaurantId üzrə işçi aidiyyətinin rolunu yeniləyir; digər restoranlardakı rollar dəyişmir. restaurantId göstərilməzsə cari restoran konteksti və ya tək aktiv aidiyyət istifadə olunur; bir neçə aidiyyət qeyri-müəyyəndirsə restoran seçmək tələb edilir. Rol dəyişəndə sessiya versiyası və icazə keşləri yenilənir. Başqa işçinin tokenləri qaytarılmır; boş accessToken/refreshToken cavabı əməliyyat xətası deyil. Platforma admini rolunu yalnız platforma admini verə bilər; mövcud sahibkar rolunun qorunması qüvvədədir."),
            ["User.GetAll"] = new("İstifadəçiləri gətir", "İstifadəçiləri səhifələmə və optional restoran filteri ilə qaytarır."),
            ["User.GetStaff"] = new("Restoran staff siyahısı", "Restorana aid owner/manager/ofisiant siyahısını qaytarır. Müştəri üçün public staff məlumatları göstərilir."),
            ["User.GetProfile"] = new("Profilimi gətir", "Token sahibi istifadəçinin profil məlumatlarını qaytarır."),
            ["User.UpdateProfile"] = new("Profilimi yenilə", "Token sahibi istifadəçinin profil məlumatlarını və şəklini yeniləyir."),
            ["User.GetStaffDetail"] = new("Staff detalı", "Restorana aid konkret staff istifadəçisinin detallı məlumatını qaytarır."),
            ["File.Upload"] = new("Fayl yüklə", "Menyu item-i, restoran, profil və müqavilə üçün icazəli faylı yükləyir və fileId qaytarır."),
            ["File.GetFile"] = new("Faylı göstər", "Token və ya query məlumatına görə faylı binary response kimi qaytarır."),
            ["RestaurantReservation.GetService"] = new("Xidmət üçün rezervasiyaları gətir", "Həmin restoranın səlahiyyətli işçisi üçün səhifələnmiş siyahı. WaitingOnly=true gəlmiş, əyləşməmiş açıq qeydləri gəliş sırası ilə göstərir. WaitingMinutes və ServiceDelayMinutes yalnız xidmət intervalında hesablanır. RequiresReceptionReview=true intervalın bitdiyini və ya müəyyən edilə bilmədiyini, nəticənin yoxlanmalı olduğunu bildirir; bu, canlı gözləmə və ya avtomatik ləğv deyil. Ofisianta maliyyə detalları qaytarılmır."),
            ["ReservationFlow.MarkArrived"] = new("Müştərinin gəlişini qeyd et", "Menecer, sahibkar və ya ofisiant müştərinin gəlişini rezervasiyanın aid olduğu iş intervalında, erkən də qeyd edə bilər. No-show tətbiq olunmur, masa sessiyası açılmır. Həmin restoranın ofisiantlarına bildiriş göndərilir. Təkrar sorğu ikinci bildiriş yaratmır."),
            ["ReservationFlow.CheckIn"] = new("Müştərini masaya əyləşdir", "Yalnız həmin restoranın ofisiantı üçün: boş, aktiv və tutumu uyğun masada sessiya açır. Erkən əyləşmə digər rezervasiyaya mane olmamalı və çıxış vaxtını qısaltmamalıdır. Ayrı gəliş qeydi tələb edilmir. Təkrar sorğu ikinci sessiya yaratmır."),
            ["ReservationFlow.RecordDeparture"] = new("Gözləyən müştərinin ayrılmasını qeyd et", "Yalnız menecer/sahibkar və gəlmiş, əyləşməmiş rezervasiya üçün. ReasonCode=1 restoran masa təmin edə bilmədi, 2 müştəri özü ayrıldı; Reason məcburidir. Səbəb tarixçə/auditdə saxlanılır və geri ödəniş hüququna uyğun ləğv olunur; no-show deyil."),
            ["ReservationFlow.ChangeTable"] = new("Gözləyən müştərinin masasını dəyiş", "Yalnız menecer/sahibkar üçün. TableId və CustomerConsent=true tələb edilir. Hər iki masa transaction daxilində kilidlənir, aktivlik/tutum/vaxt uyğunluğu yenidən yoxlanılır. Maliyyə və çıxış şərtləri dəyişmir, sessiya açılmır."),
            ["MobileApp.GetRestaurantModule"] = new("Restoranın mobil ayarlarını gətir", "Yalnız platforma admini üçün: restoran işçilərinin mobil tətbiqə giriş və push ayarlarını qaytarır."),
            ["MobileApp.UpdateRestaurantModule"] = new("Restoranın mobil ayarlarını yenilə", "Yalnız platforma admini üçün: ShowDownloadLink restoranın işçi tətbiqinə giriş hüququdur. Bağlananda push da sönür. Push yalnız aktiv giriş və hazır çatdırılma olduqda açıla bilər."),
            ["MobileApp.GetPublication"] = new("Köhnə yayımlanma vəziyyətini gətir", "Public yayımlanma daim bağlıdır. ReleaseReady yalnız şəxsi APK faylının ölçüsü və SHA-256 dəyəri yoxlananda true olur."),
            ["MobileApp.UpdatePublication"] = new("Köhnə public yayımlanma ayarını bağla", "PublicDownloadEnabled=true artıq qəbul edilmir. false köhnə qlobal ayarı bağlayır."),
            ["MobileApp.GetPublicRelease"] = new("Köhnə ümumi buraxılış endpointi", "Həmişə IsVisible=false qaytarır; APK URL-ni göstərmir."),
            ["MobileApp.GetPublicRestaurantRelease"] = new("Köhnə restoran buraxılışı endpointi", "Həmişə IsVisible=false qaytarır; APK URL-ni göstərmir."),
            ["MobileApp.GetCustomerAccess"] = new("Müştərinin mobil girişini yoxla", "Bearer token ilə cari hesabın bazada aktiv Customer rolunda olduğunu yoxlayır. Uğurda { enabled: true }, başqa rol və ya deaktiv hesab üçün 403 qaytarır. Restoran işçisi üçün ayrıca restoran giriş endpointindən istifadə edin."),
            ["MobileApp.GetCustomerRelease"] = new("Müştəri üçün APK məlumatını gətir", "Bearer token və bazada aktiv Customer hesabı tələb edir. Restoran icazəsindən asılı deyil. Doğrulanmış private APK varsa Ready=true, DownloadPath, versiya, ölçü və SHA-256 qaytarır; yoxdursa Ready=false. Anonim sorğu 401, başqa rol və deaktiv hesab 403 alır."),
            ["MobileApp.DownloadCustomerRelease"] = new("Müştəri üçün APK-ni yüklə", "Hər sorğuda cari hesabın bazada aktiv Customer rolunda olduğunu yenidən yoxlayır. Doğrulanmış APK-ni private/no-store cavabla stream edir; public URL yoxdur. Anonim sorğu 401, başqa rol və deaktiv hesab 403, buraxılış hazır deyilsə 404 alır."),
            ["MobileApp.GetStaffAccess"] = new("İşçinin mobil tətbiq girişini yoxla", "Bearer token və həmin restoranda aktiv Owner, Manager, Waiter və ya Kitchen təyinatı tələb edir. Restoran girişi və aktiv müqavilə yoxdursa Enabled=false, aidiyyəti olmayan istifadəçiyə 403 qaytarır."),
            ["MobileApp.GetStaffRelease"] = new("İşçi üçün APK məlumatını gətir", "Restoran icazəsini yoxlayır. Şəxsi server faylı doğrulanıbsa qorunan DownloadPath və versiyanı qaytarır; əks halda Ready=false."),
            ["MobileApp.DownloadStaffRelease"] = new("İşçi üçün APK-ni yüklə", "Hər sorğuda işçi təyinatını, aktiv müqaviləni və mobil girişi yoxlayır. APK-ni private/no-store cavabla stream edir; public URL yoxdur."),
            ["MobileApp.RegisterInstallation"] = new("Telefon bildirişi cihazını qeydiyyata al", "Aktiv müştəri və ya adminin mobil giriş və push icazəsi verdiyi aktiv, müqaviləsi qüvvədə olan restoranın aktiv Owner, Manager, Waiter və ya Kitchen işçisi üçün. installationId, ExpoPushToken və ExpoProjectId ilə aktiv sessiyaya bağlı cihazı qeydiyyata alır və ya yeniləyir. Müştəri yalnız öz rezervasiya, geri ödəniş və sifariş bildirişlərini alır; onun üçün restoranın işçi mobil modulu tələb olunmur. İcazəsiz hesab 403 alır. Serverdə push bağlıdırsa və ya layihə ID-si uyğun gəlmirsə sorğu rədd edilir. Token cavabda qaytarılmır. Göndərişdən əvvəl hesab, sessiya, obyekt aidiyyəti və işçi restoran icazəsi təkrar yoxlanılır."),
            ["MobileApp.DeactivateInstallation"] = new("Push cihazının qeydiyyatını bağla", "Giriş etmiş istifadəçinin cari sessiyasına aid installationId-ni deaktiv edir və 204 qaytarır. Qeydiyyat tapılmasa da 204 qaytarılır; başqa istifadəçinin və ya sessiyanın cihazı dəyişdirilmir.")
        };

    public static string GetTagName(string controllerName)
    {
        return Tags.TryGetValue(controllerName, out var tag)
            ? tag.Name
            : controllerName;
    }
}

public sealed record SwaggerTagInfo(string Name, string Description);

public sealed record SwaggerEndpointInfo(string Summary, string Description);
