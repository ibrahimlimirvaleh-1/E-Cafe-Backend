using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class RoleSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            var roles = Enum
                .GetValues<RoleCode>()
                .Select(role => new Domain.Entities.Role
                {
                    Id = (int)role,
                    Name = role.GetName()
                })
                .ToList();

            modelBuilder.Entity<Domain.Entities.Role>().HasData(roles);
        }
    }
}