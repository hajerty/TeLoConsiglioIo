using Microsoft.AspNetCore.Identity;
using TeLoConsiglio.Domain.Entities;

namespace TeLoConsiglio.Api.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, ILogger logger)
    {
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var r in Roles.All)
        {
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));
        }

        const string adminEmail = "admin@teloconsiglio.io";
        const string adminPassword = "Admin!2026";

        var admin = await userMgr.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Administrator"
            };
            var res = await userMgr.CreateAsync(admin, adminPassword);
            if (!res.Succeeded)
            {
                logger.LogError("Seed admin fallito: {Errors}",
                    string.Join("; ", res.Errors.Select(e => e.Description)));
                return;
            }
            await userMgr.AddToRoleAsync(admin, Roles.Admin);
            logger.LogWarning(
                "SICUREZZA: utente admin di default creato {Email} con password '{Password}'. " +
                "CAMBIA SUBITO LA PASSWORD IN PRODUZIONE.", adminEmail, adminPassword);
        }
    }
}
