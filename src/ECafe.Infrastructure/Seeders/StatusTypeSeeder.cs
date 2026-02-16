using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class StatusTypeSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            var statusTypes = new List<Domain.Entities.StatusType>();

            foreach (var statusType in Enum.GetValues<Domain.Enums.StatusType>())
            {
                statusTypes.Add(new Domain.Entities.StatusType
                {
                    Id = (int)statusType,
                    Name = statusType.GetName(),
                });
            }

        }

    }
}
