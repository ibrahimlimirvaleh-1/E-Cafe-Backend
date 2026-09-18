# Rezervasiya Payment Instructions Review

Bu sənəd `payment-instructions` endpoint-i üçün son dəyişikliklərdə düzəldilməli hissələri göstərir.

## Hazırkı kritik problemlər

### 1. Controller endpoint-i yoxdur

`ReservationController`-ə aşağıdakı endpoint əlavə edilməlidir:

```http
POST /api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions
```

Endpoint request-i mediator və ya reservation service-ə ötürməlidir.

### 2. Repository contract uyğunsuzdur

Hazırda interface `bool`, implementation isə `Reservation?` qaytarır. Bu compile problemidir.

Düzgün contract:

```csharp
Task<Reservation?> GetByIdForRestaurantAsync(
    int reservationId,
    int restaurantId,
    CancellationToken cancellationToken = default);
```

Repository implementation:

```csharp
public Task<Reservation?> GetByIdForRestaurantAsync(
    int reservationId,
    int restaurantId,
    CancellationToken cancellationToken = default)
{
    return QueryTracked()
        .Include(x => x.Status)
        .FirstOrDefaultAsync(
            x => x.Id == reservationId &&
                 x.RestaurantId == restaurantId,
            cancellationToken);
}
```

### 3. Reservation status ID səhv hesablanır

Bu düzgün deyil:

```csharp
(int)ReservationStatus.PendingPayment
```

Bu yalnız enum dəyəri olan `1` qaytarır. Database status ID-si composite-dir.

Düzgün variant:

```csharp
var pendingPaymentStatusId =
    StatusIds.Reservation(ReservationStatus.PendingPayment);
```

### 4. Status və expiry yoxlaması səhvdir

Bu şərt düzgün deyil:

```csharp
if (reservation.StatusId != pendingPaymentStatusId &&
    reservation.HoldExpiresAt < now)
```

Aşağıdakı hallardan biri baş verərsə əməliyyat bloklanmalıdır:

- status `PendingPayment` deyil;
- `HoldExpiresAt` boşdur;
- hold vaxtı keçib.

Düzgün variant:

```csharp
if (reservation.StatusId != pendingPaymentStatusId ||
    reservation.HoldExpiresAt is null ||
    reservation.HoldExpiresAt <= nowUtc)
{
    throw new BusinessRuleException(
        ErrorCode.ThisOperationCannotBePerformedForThisReservation);
}
```

### 5. Payment instruction rezervasiya ilə əlaqələndirilməlidir

Menecerin yazdığı mesaj konkret rezervasiyaya aiddir. Bir rezervasiya üçün bir instruction, başqa rezervasiya üçün isə tamamilə fərqli instruction yaratmaq düzgündür. Kart nömrəsi və ya mesaj `reservations` cədvəlinə yazılmamalıdır.

Hazırkı problemin səbəbi `RestaurantPaymentInstruction` entity-sində `ReservationId` əlaqəsinin olmamasıdır. Kod yeni instruction yaradır, amma onun hansı rezervasiyaya aid olduğu bilinmir.

İki düzgün seçim var:

```text
1. RestaurantPaymentInstruction entity-sinə ReservationId əlavə etmək
2. Entity və cədvəli ReservationPaymentInstruction adlandırmaq
```

İkinci adlandırma biznes baxımından daha aydındır. Tövsiyə olunan cədvəl:

```text
reservation_payment_instructions
```

Sahələr:

```text
id
reservation_id
display_text
amount
sent_by_user_id
sent_at
```

Burada `reservation_id` foreign key olmalıdır. `restaurant_id` ayrıca saxlamaq məcburi deyil, çünki restoran rezervasiya üzərindən müəyyən edilir. Bu snapshot yanaşması ilə başqa rezervasiya üçün yazılan mesaj əvvəlki rezervasiyaya təsir etmir.

### 6. Rol yoxlaması kifayət deyil

Yalnız restoran assignment-ının olması kifayət deyil. Ofisiant bu endpoint-i çağıra bilməməlidir.

İcazəli rollar:

```text
Owner
Manager
```

`UserBelogsToRestaurantAsync` əvəzinə aktiv rol yoxlanılmalıdır:

```csharp
var roleId = await _userRestaurantRepository
    .GetActiveRoleIdAsync(userId, restaurantId);

if (roleId is not ((int)RoleCode.Owner) and not ((int)RoleCode.Manager))
{
    throw new ForbiddenException();
}
```

### 7. Notification çatışmır

Mesaj bazaya yazıldıqdan sonra müştəriyə in-app notification göndərilməlidir. Notification transaction commit-dən sonra yaradılmalıdır və payload-da bunlar olmalıdır:

```json
{
  "restaurantId": 1,
  "reservationId": 25
}
```

### 8. Transaction çatışmır

Payment instruction snapshot-ı və onun notification/outbox məlumatı birlikdə idarə olunmalıdır. Database yazılması uğursuz olarsa müştəriyə notification getməməlidir.

### 9. Metod adı və return problemi

Bu ad yanlışdır:

```csharp
SentPaymentInstructionAsync
```

Düzgün ad:

```csharp
SendPaymentInstructionAsync
```

Metod `PaymentInstructionResponse` qaytarmalıdır. `return` olmadan metod tamamlanmamalıdır.

## Request modeli

Müştərinin məlumatı görməsi üçün menecer yalnız mesaj göndərir:

```csharp
public sealed class PaymentInstructionRequest
{
    public string DisplayText { get; init; } = string.Empty;
}
```

Validation:

```csharp
RuleFor(x => x.DisplayText)
    .NotEmpty()
    .MaximumLength(2000);
```

CVV, OTP, internet bankçılıq şifrəsi və digər gizli məlumatlar qəbul edilməməlidir.

## Status davranışı

Bu endpoint statusu dəyişmir:

```text
PendingPayment -> PendingPayment
```

Status yalnız müştəri payment proof göndərəndə dəyişir:

```text
PendingPayment -> PaymentSubmitted
```

Menecer çekə baxıb təsdiq etdikdə:

```text
PaymentSubmitted -> Confirmed
```

## Response modeli

```json
{
  "reservationId": 25,
  "status": "PendingPayment",
  "displayText": "Depoziti ABB kartına köçürün. Ödənişdən sonra çeki göndərin.",
  "amount": 100.00,
  "sentAt": "2026-09-17T12:10:00Z",
  "holdExpiresAt": "2026-09-17T12:25:00Z"
}
```

Tarixlər UTC formatında qaytarılmalıdır.

## Test ssenariləri

1. Owner payment instruction göndərə bilir.
2. Manager payment instruction göndərə bilir.
3. Ofisiant `403` alır.
4. Başqa restorana aid rezervasiyaya mesaj göndərilə bilmir.
5. `Confirmed` rezervasiyaya mesaj göndərilə bilmir.
6. `Expired` rezervasiyaya mesaj göndərilə bilmir.
7. Hold vaxtı keçmiş rezervasiyaya mesaj göndərilə bilmir.
8. Boş və 2000 simvoldan uzun mesaj qəbul edilmir.
9. Snapshot konkret rezervasiyaya bağlanır.
10. Müştəri notification alır.
11. Eyni request təkrar göndərildikdə duplicate davranışı idarə olunur.
12. Response-da məbləğ və expiry vaxtı düzgün qaytarılır.

## Düzəliş ardıcıllığı

1. `ReservationPaymentInstruction` entity və configuration yarat.
2. Migration yarat.
3. Repository contract və implementation yaz.
4. Request validator əlavə et.
5. `SendPaymentInstructionAsync` servis metodunu yaz.
6. Owner/Manager authorization əlavə et.
7. Transaction və notification əlavə et.
8. Controller endpoint-i əlavə et.
9. Integration test yaz.
10. Frontend-də menecer üçün mesaj formu və müştəri üçün payment məlumatı göstər.
