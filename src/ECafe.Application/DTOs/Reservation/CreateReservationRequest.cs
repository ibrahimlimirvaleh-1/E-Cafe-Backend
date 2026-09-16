namespace ECafe.Application.DTOs.Reservation
{
    public class CreateReservationRequest
    {
        public int TableId {  get; set; }

        public DateTimeOffset ReservedAt { get; set; }

        public int PeopleCount { get; set; }

        public string? Note { get; set; }

    }
}
