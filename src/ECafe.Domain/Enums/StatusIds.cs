namespace ECafe.Domain.Enums
{
    public static class StatusIds
    {
        private const int StatusTypeMultiplier = 1000;

        public static int Reservation(ReservationStatus status)
            => Build(StatusType.Reservation, (int)status);

        public static int Contract(ContractStatus status)
            => Build(StatusType.Contract, (int)status);

        public static int TableSession(TableSessionStatus status)
            => Build(StatusType.TableSession, (int)status);

        public static int Item(ItemStatus status)
            => Build(StatusType.ItemStatus, (int)status);

        public static int Build(StatusType type, int status)
            => ((int)type * StatusTypeMultiplier) + status;
    }
}
