namespace ECafe.Application.Common.Validation;

public static class RestaurantDepositAmount
{
    public const string InvalidAmountMessage =
        "Tarix üzrə depozit 0-dan böyük, 100000 AZN-dən çox olmayan və iki onluq rəqəmli məbləğ olmalıdır.";

    public static bool IsValid(decimal amount)
        => amount > 0 && amount <= 100000m && decimal.Round(amount, 2) == amount;
}
