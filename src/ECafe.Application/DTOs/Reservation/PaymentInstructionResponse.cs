namespace ECafe.Application.DTOs.Reservation
{
    public class PaymentInstructionResponse
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }

        public string Status { get; set; } = null!;

        public string DisplayText { get; set; } = null!;

        public decimal Amount { get; set; }

        public DateTime SentAt { get; set; }
    }
}
