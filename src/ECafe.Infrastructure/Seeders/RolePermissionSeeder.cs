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


            // Customer
            AddPermissions(rolePermissions, RoleCode.Customer, new[]
            {
                PermissionCode.ViewRestaurantInfo
            });
            // Admin
            AddPermissions(rolePermissions, RoleCode.SuperAdmin, new[]
            {
                PermissionCode.ManageStaff,
                PermissionCode.ManageUsers,
                PermissionCode.ManageRestaurants,
                PermissionCode.ManageCatalog,
                PermissionCode.ManageTables,
                PermissionCode.ManageReservations,
                PermissionCode.ManageOrders,
                PermissionCode.ManagePayments,
                PermissionCode.ViewReports,
                PermissionCode.ViewRestaurantInfo,
                PermissionCode.ManageRestaurantContracts,
                PermissionCode.ViewRestaurantContracts,
                PermissionCode.AssignRoles,
            });

            //Owner
            // Manager
            AddPermissions(rolePermissions, RoleCode.Owner, new[]
            {
                PermissionCode.ManageStaff,
                PermissionCode.ViewReports,
                PermissionCode.ViewRestaurantInfo,
                PermissionCode.ViewRestaurantContracts

            });
            // Manager
            AddPermissions(rolePermissions, RoleCode.Manager, new[]
            {
                PermissionCode.ManageStaff,
                PermissionCode.ManageCatalog,
                PermissionCode.ManageTables,
                PermissionCode.ManageReservations,
                PermissionCode.ManageOrders,
                PermissionCode.ManagePayments,
                PermissionCode.ViewReports,
                PermissionCode.ViewRestaurantInfo,
                PermissionCode.ViewRestaurantContracts

            });

            // Waiter
            AddPermissions(rolePermissions, RoleCode.Waiter, new[]
            {
                PermissionCode.ViewRestaurantInfo,
                PermissionCode.ViewAssignedReservations,
                PermissionCode.ManageOrders,
                PermissionCode.ManagePayments,
                PermissionCode.ViewOwnWallet
            });

            // Kitchen
            AddPermissions(rolePermissions, RoleCode.Kitchen, new[]
            {
                PermissionCode.ViewRestaurantInfo,
                PermissionCode.ManageKitchenOrders
            });

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