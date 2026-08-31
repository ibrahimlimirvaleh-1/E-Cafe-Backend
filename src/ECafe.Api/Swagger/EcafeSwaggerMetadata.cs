namespace ECafe.Api.Swagger;

public static class EcafeSwaggerMetadata
{
    public static readonly IReadOnlyDictionary<string, SwaggerTagInfo> Tags =
        new Dictionary<string, SwaggerTagInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Auth"] = new("01. Authentication", "Login, qeydiyyat və refresh token əməliyyatları."),
            ["Restaurant"] = new("02. Restaurants", "Restoran kataloqu, restoran profili və public booking üçün əsas məlumatlar."),
            ["Category"] = new("03. Menu Categories", "Restoran menyu kateqoriyalarının idarə olunması."),
            ["Item"] = new("04. Menu Items", "Menyu məhsulları, qiymət, status və şəkil məlumatları."),
            ["Table"] = new("05. Tables", "Restoran stollarının yaradılması və gələcək availability axınının bazası."),
            ["User"] = new("06. Users & Staff", "İstifadəçi, staff, ofisiant və profil əməliyyatları."),
            ["File"] = new("07. Files", "Şəkil və fayl yükləmə/göstərmə endpoint-ləri.")
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
            ["File.GetFile"] = new("Faylı göstər", "Token və ya query məlumatına görə faylı binary response kimi qaytarır.")
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
