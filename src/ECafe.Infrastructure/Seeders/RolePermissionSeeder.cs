using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Infrastructure.Seeders
{
    public static class RolePermissionSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            var rolePermissions = new List<RolePermission>();

            // Admin
            AddPermissions(rolePermissions, RoleCode.SuperAdmin, new[]
            {
                PermissionCode.ManageUsers,
                PermissionCode.ManageRestaurants,
                PermissionCode.ManageCatalog,
                PermissionCode.ManageTables,
                PermissionCode.ManageReservations,
                PermissionCode.ManageOrders,
                PermissionCode.ManagePayments,
                PermissionCode.ViewReports
            });

            // Manager
            AddPermissions(rolePermissions, RoleCode.Manager, new[]
            {
                PermissionCode.ManageCatalog,
                PermissionCode.ManageTables,
                PermissionCode.ManageReservations,
                PermissionCode.ManageOrders,
                PermissionCode.ManagePayments,
                PermissionCode.ViewReports
            });

            // Waiter -> hazırkı permission modelində ayrıca uyğun permission yoxdur
            // Customer -> permission seed verilmir

            modelBuilder.Entity<RolePermission>().HasData(rolePermissions);
        }

        private static void AddPermissions(
            List<RolePermission> rolePermissions,
            RoleCode role,
            IEnumerable<PermissionCode> permissions)
        {
            foreach (var permission in permissions)
            {
                rolePermissions.Add(new RolePermission
                {
                    RoleId = (int)role,
                    PermissionId = (int)permission
                });
            }
        }
    }
}