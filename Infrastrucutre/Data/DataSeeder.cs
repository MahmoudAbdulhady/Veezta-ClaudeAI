using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastrucutre.Data
{
    public class DataSeeder
    {
        public static async Task SeedData(UserManager<CustomUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            await SeedRoles(roleManager);
            await SeedAdminUser(userManager);
        }


        private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {

            var roles = new List<string>
    {
        "Admin",
        "Doctor",
        "Patient"
    };

            foreach (var roleName in roles)
                {
                    if (!await roleManager.RoleExistsAsync(roleName))
                    {
                        var role = new IdentityRole(roleName);
                        await roleManager.CreateAsync(role);
                    }
                }
            }
        

        private static async Task SeedAdminUser(UserManager<CustomUser> userManager)
        {
            var adminEmail = "veeztadmin@test.com";
            var adminPassword = "Admin@123"; // change as you like

            var existingUser = await userManager.FindByEmailAsync(adminEmail);

            if (existingUser == null)
            {
                var adminUser = new CustomUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Veezta",
                    LastName = "Admin",
                    FullName = "Veezta Admin",
                    DateOfBirth = new DateTime(1999, 9, 24),
                    Gender = Gender.Male,
                    ImageUrl = "Admin",
                    AccountRole = AccountRole.Admin,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(" | ", createResult.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to create admin user: {errors}");
                }

                var roleResult = await userManager.AddToRoleAsync(adminUser, AccountRole.Admin.ToString());

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(" | ", roleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to assign admin role: {errors}");
                }
            }
        }

        public static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in Enum.GetNames(typeof(AccountRole)))
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
