# Sahibkar Axtarışı Üçün User Response Düzəlişi

## Məqsəd

Restoran yaratma səhifəsində mövcud sahibkar email və ya telefonla axtarılanda bazada olan sahibkar tapılmırdı. Məsələn, `role_id = 2` olan `eminovf5@gmail.com` istifadəçisi bazada var idi, amma frontend "Bu məlumatla sahibkar tapılmadı" mesajı göstərirdi.

## Kök Səbəb

`GET /api/v1/admin/users` endpoint-i `GetAllUserResponseDto` ilə paginated istifadəçi siyahısı qaytarırdı, amma DTO-da axtarış üçün lazım olan `Email` və `Phone` sahələri yox idi.

Eyni zamanda rol məlumatı əsasən nested `Role` obyekti kimi qaytarılırdı. Frontend sahibkarı stabil tanımaq üçün top-level `RoleId` və oxunaqlı `RoleName` sahələrinə də ehtiyac duyur.

## Edilən Dəyişikliklər

### `src/ECafe.Application/DTOs/User/GetAllUserResponseDto.cs`

DTO-ya bu sahələr əlavə edildi:

- `Email`
- `Phone`
- `RoleId`
- `RoleName`

### `src/ECafe.Application/Mappings/UserProfile.cs`

`UserRestaurant -> GetAllUserResponseDto` mapping-i genişləndirildi:

- `Email` user-dən götürülür.
- `Phone` user-dən götürülür.
- `RoleId` assignment üzərindəki roldan götürülür.
- `RoleName` assignment rolunun adından götürülür.

## Niyə Belə Daha Düzgündür?

- Frontend mövcud sahibkarı email/telefonla real backend response üzərindən tapa bilir.
- Nested `Role` obyekti saxlanıldığı üçün əvvəlki response formatı pozulmur.
- Top-level `RoleId` və `RoleName` əlavə edildiyi üçün frontend daha sadə və stabil map edə bilir.
- Restoran kontekstində user başqa restoranda fərqli rol daşıya bildiyi üçün `UserRestaurant.RoleId` əsas götürülür.

## Təsir Dairəsi

- Endpoint response-u geriyə uyğun genişləndirildi.
- Mövcud field-lər silinmədi.
- Database migration tələb olunmur, çünki yalnız DTO və mapping dəyişib.

## Risk

Email və telefon admin endpoint-də görünür. Bu endpoint artıq `ManageUsers` permission ilə qorunduğu üçün məlumat yalnız idarəetmə səlahiyyəti olan istifadəçilərə açıqdır.

