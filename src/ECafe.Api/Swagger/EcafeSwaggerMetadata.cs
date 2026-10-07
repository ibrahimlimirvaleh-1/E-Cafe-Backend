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
            ["User.Create"] = new("Staff istifadəçisi yarat", "Owner/manager/ofisiant kimi restoran staff hesabı yaradır və restorana bağlayır."),
            ["User.Delete"] = new("İstifadəçini sil", "Staff istifadəçisini soft-delete edir."),
            ["User.UpdateRole"] = new("İstifadəçi rolunu dəyiş", "Staff istifadəçisinin rolunu yeniləyir."),
            ["User.GetAll"] = new("İstifadəçiləri gətir", "İstifadəçiləri səhifələmə və optional restoran filteri ilə qaytarır."),
            ["User.GetStaff"] = new("Restoran staff siyahısı", "Restorana aid owner/manager/ofisiant siyahısını qaytarır. Müştəri üçün public staff məlumatları göstərilir."),
            ["User.GetProfile"] = new("Profilimi gətir", "Token sahibi istifadəçinin profil məlumatlarını qaytarır."),
            ["User.UpdateProfile"] = new("Profilimi yenilə", "Token sahibi istifadəçinin profil məlumatlarını və şəklini yeniləyir."),
            ["User.GetStaffDetail"] = new("Staff detalı", "Restorana aid konkret staff istifadəçisinin detallı məlumatını qaytarır."),
            ["File.Upload"] = new("Fayl yüklə", "Menyu item-i, restoran, profil və müqavilə üçün icazəli faylı yükləyir və fileId qaytarır."),
            ["File.GetFile"] = new("Faylı göstər", "Token və ya query məlumatına görə faylı binary response kimi qaytarır."),
            ["RestaurantReservation.GetService"] = new("Xidmət üçün rezervasiyaları gətir", "Həmin restoranın səlahiyyətli işçisi üçün təsdiqlənmiş və əyləşdirilmiş rezervasiyaların səhifələnmiş siyahısı. Ofisianta ödəniş, depozit və geri ödəniş detalları qaytarılmır."),
            ["ReservationFlow.MarkArrived"] = new("Müştərinin gəlişini qeyd et", "Menecer, sahibkar və ya ofisiant müştərinin restoranda olduğunu vaxt pəncərəsi daxilində təsdiqləyir. Rezervasiya təsdiqlənmiş qalır, no-show tətbiq olunmur, masa sessiyası açılmır. Təkrar sorğu gəliş vaxtını dəyişmir və ikinci bildiriş göndərmir."),
            ["ReservationFlow.CheckIn"] = new("Müştərini masaya əyləşdir", "Yalnız həmin restoranın ofisiantı üçün: masa boşdursa rezervasiyanı Seated statusuna keçirir, masa sessiyası açır və ofisiantı bağlayır. Ayrı gəliş qeydi tələb edilmir; əvvəldən gəlmiş müştəri no-show vaxtından sonra da masanı təhvil vermə müddəti bitməyibsə əyləşdirilə bilər."),
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
            ["MobileApp.RegisterInstallation"] = new("Push cihazını qeydiyyata al", "Giriş etmiş istifadəçinin aktiv sessiyası üçün installationId ilə Expo push tokenini qeydiyyata alır və ya yeniləyir. Body-də ExpoPushToken və ExpoProjectId tələb olunur; serverdə push hazır deyilsə və ya layihə ID-si uyğun gəlmirsə sorğu rədd edilir. Token cavabda qaytarılmır."),
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
