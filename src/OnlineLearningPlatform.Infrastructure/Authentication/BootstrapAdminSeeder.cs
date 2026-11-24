using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Authentication;

/// <summary>Creates the first administrator from deployment secrets, but never overwrites an existing admin.</summary>
public sealed class BootstrapAdminSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<BootstrapAdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Bootstrap admin credentials are not configured; no administrator was seeded.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Both BootstrapAdmin:Email and BootstrapAdmin:Password must be configured together.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!dbContext.Database.IsRelational())
        {
            // Test providers have no migration job; production databases are migrated before the API starts.
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            var roleResult = await roleManager.CreateAsync(new ApplicationRole(Roles.Admin));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException("The Admin role could not be created for bootstrap.");
            }
        }

        var existingAdmins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        if (existingAdmins.Count > 0)
        {
            logger.LogInformation("An administrator already exists; bootstrap will not modify administrator credentials.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException(
                "The configured bootstrap email belongs to a non-admin account. Configure a unique administrator email.");
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true,
            MustChangePassword = true,
        };

        var createResult = await userManager.CreateAsync(admin, password);
        if (!createResult.Succeeded)
        {
            var codes = string.Join(",", createResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"The bootstrap administrator could not be created. Identity error codes: {codes}");
        }

        var addRoleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(admin);
            var codes = string.Join(",", addRoleResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"The bootstrap administrator role could not be assigned. Identity error codes: {codes}");
        }

        logger.LogInformation("Bootstrap administrator created. The first successful login must change its password.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
