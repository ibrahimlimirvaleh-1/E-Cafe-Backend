using ECafe.Domain.Enums;
using ECafe.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class PermissionSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            var permissions = Enum
                .GetValues<PermissionCode>()
                .Select(permission => new Domain.Entities.Permission
                {
                    Id = (int)permission,
                    Name = permission.GetName()
                })
                .ToList();

            modelBuilder.Entity<Domain.Entities.Permission>().HasData(permissions);
        }
    }
}