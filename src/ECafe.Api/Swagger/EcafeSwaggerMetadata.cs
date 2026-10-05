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
            ["MobileApp.GetRestaurantModule"] = new("Restoranın mobil ayarlarını gətir", "Yalnız platforma admini üçün: restoran üzrə push bildirişlərinin aktivliyini və mobil tətbiqin yükləmə linkinin göstərilib-göstərilmədiyini qaytarır."),
            ["MobileApp.UpdateRestaurantModule"] = new("Restoranın mobil ayarlarını yenilə", "Yalnız platforma admini üçün: MobilePushEnabled və ShowDownloadLink ayarlarını yeniləyir. Push yalnız serverdə çatdırılma hazır olduqda və Expo layihəsi konfiqurasiya edildikdə aktivləşdirilə bilər."),
            ["MobileApp.GetPublication"] = new("Mobil tətbiqin yayımlanma vəziyyətini gətir", "Yalnız platforma admini üçün: ümumi yükləmə linkinin açıq olub-olmadığını və APK buraxılış konfiqurasiyasının hazır olub-olmadığını qaytarır."),
            ["MobileApp.UpdatePublication"] = new("Ümumi yükləmə linkini idarə et", "Yalnız platforma admini üçün: PublicDownloadEnabled ayarını dəyişir. APK buraxılış konfiqurasiyası hazır deyilsə ümumi yükləmə linkini aktivləşdirmək olmaz."),
            ["MobileApp.GetPublicRelease"] = new("Ümumi mobil buraxılışı gətir", "Giriş tələb etmir. Ümumi yayımlanma aktivdirsə, APK buraxılış konfiqurasiyası hazırdırsa və ən azı bir uyğun restoran linki göstərirsə yükləmə URL-i, versiya, ölçü və SHA-256 məlumatını qaytarır; əks halda IsVisible=false qaytarır."),
            ["MobileApp.GetPublicRestaurantRelease"] = new("Restoranın mobil buraxılışını gətir", "Giriş tələb etmir. Restoran aktivdirsə, aktiv müqaviləsi və yükləmə linki ayarı varsa, hazır APK buraxılışının URL, versiya, ölçü və SHA-256 məlumatını qaytarır; əks halda IsVisible=false qaytarır. Ümumi yayımlanma ayarından asılı deyil."),
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
