namespace ECafe.Application.DTOs.Restaurant;

public class RestaurantWorkingHourDto
{
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpensAt { get; set; } = new(9, 0);

    public TimeOnly ClosesAt { get; set; } = new(0, 0);

    public bool IsClosed { get; set; }
}

public class RestaurantOpenStateDto
{
    public bool IsOpen { get; set; }

    public string OpenStatus { get; set; } = "Closed";

    public RestaurantWorkingHourDto? TodayWorkingHours { get; set; }
}
