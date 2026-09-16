using ECafe.Application.Repositories.Reservation;
using ECafe.Infrastructure.Context;

namespace ECafe.Infrastructure.Repositories.Reservation
{
    public class ReservationRepository : BaseRepository<Domain.Entities.Reservation>, IReservationRepository
    {
        public ReservationRepository(ECafeDbContext context) : base(context)
        {
        }
    }
}
