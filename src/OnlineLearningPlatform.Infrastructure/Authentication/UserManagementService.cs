using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure.Authentication;

public sealed class UserManagementService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    IProfileImageStorage profileImageStorage,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<UserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.TimeZoneId,
            user.EmailConfirmed,
            user.ProfileImageUrl,
            roles.ToArray());
    }

    public async Task<UserProfileResponse?> UpdateProfileAsync(
        Guid userId,
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.TimeZoneId = request.TimeZoneId.Trim();
        EnsureIdentitySucceeded(await userManager.UpdateAsync(user), "Could not update the user's profile.");

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfileResponse?> UploadProfileImageAsync(
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var uploaded = await profileImageStorage.UploadAsync(userId, fileName, content, cancellationToken);
        var previousPublicId = user.ProfileImagePublicId;
        user.ProfileImageUrl = uploaded.SecureUrl;
        user.ProfileImagePublicId = uploaded.PublicId;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await TryDeleteProfileImageAsync(uploaded.PublicId, userId, cancellationToken);
            EnsureIdentitySucceeded(updateResult, "Could not update the user's profile image.");
        }

        if (!string.IsNullOrWhiteSpace(previousPublicId))
        {
            await TryDeleteProfileImageAsync(previousPublicId, userId, cancellationToken);
        }

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<UserProfileResponse?> RemoveProfileImageAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var previousPublicId = user.ProfileImagePublicId;
        user.ProfileImageUrl = null;
        user.ProfileImagePublicId = null;
        EnsureIdentitySucceeded(await userManager.UpdateAsync(user), "Could not remove the user's profile image.");

        if (!string.IsNullOrWhiteSpace(previousPublicId))
        {
            await TryDeleteProfileImageAsync(previousPublicId, userId, cancellationToken);
        }

        return await GetProfileAsync(userId, cancellationToken);
    }

    public async Task<PagedAdminUsersResponse> ListUsersAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<ApplicationUser> users = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            users = users.Where(user =>
                (user.Email != null && user.Email.Contains(search)) ||
                user.FirstName.Contains(search) ||
                user.LastName.Contains(search));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var page = await users
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var summaries = new List<AdminUserSummary>(page.Count);
        foreach (var user in page)
        {
            var roles = await userManager.GetRolesAsync(user);
            summaries.Add(new AdminUserSummary(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.MustChangePassword,
                roles.ToArray()));
        }

        return new PagedAdminUsersResponse(query.Page, query.PageSize, totalCount, summaries);
    }

    public Task<UserManagementOutcome> ChangeRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        string role,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var canonicalRole = Roles.All.Single(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));
        return ExecuteTransactionAsync(async () =>
        {
            var target = await userManager.FindByIdAsync(targetUserId.ToString());
            if (target is null)
            {
                return UserManagementOutcome.NotFound;
            }

            var currentRoles = (await userManager.GetRolesAsync(target)).ToArray();
            if (currentRoles.Length == 1 && currentRoles[0] == canonicalRole)
            {
                return UserManagementOutcome.Succeeded;
            }

            if (target.IsActive && currentRoles.Contains(Roles.Admin) && canonicalRole != Roles.Admin &&
                !await HasOtherActiveAdminAsync(target.Id))
            {
                return UserManagementOutcome.LastActiveAdmin;
            }

            if (currentRoles.Length > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(target, currentRoles);
                EnsureIdentitySucceeded(removeResult, "Could not remove the user's existing roles.");
            }

            var addResult = await userManager.AddToRoleAsync(target, canonicalRole);
            EnsureIdentitySucceeded(addResult, "Could not assign the requested user role.");
            EnsureIdentitySucceeded(await userManager.UpdateSecurityStampAsync(target), "Could not invalidate old access tokens.");
            await RevokeRefreshSessionsAsync(target.Id, "RoleChanged", cancellationToken);

            dbContext.AuditLogs.Add(AuditLog.Create(
                timeProvider.GetUtcNow().UtcDateTime,
                "UserRoleChanged",
                "ApplicationUser",
                actorUserId,
                target.Id.ToString(),
                $"Role changed from [{string.Join(",", currentRoles)}] to [{canonicalRole}].",
                ipAddress));
            await dbContext.SaveChangesAsync(cancellationToken);
            return UserManagementOutcome.Succeeded;
        }, cancellationToken);
    }

    public Task<UserManagementOutcome> ChangeStatusAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isActive,
        string? ipAddress,
        CancellationToken cancellationToken) => ExecuteTransactionAsync(async () =>
    {
        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return UserManagementOutcome.NotFound;
        }

        if (target.IsActive == isActive)
        {
            return UserManagementOutcome.Succeeded;
        }

        var roles = await userManager.GetRolesAsync(target);
        if (!isActive && target.IsActive && roles.Contains(Roles.Admin) && !await HasOtherActiveAdminAsync(target.Id))
        {
            return UserManagementOutcome.LastActiveAdmin;
        }

        target.IsActive = isActive;
        EnsureIdentitySucceeded(await userManager.UpdateAsync(target), "Could not update the user's account status.");
        EnsureIdentitySucceeded(await userManager.UpdateSecurityStampAsync(target), "Could not invalidate old access tokens.");
        if (!isActive)
        {
            await RevokeRefreshSessionsAsync(target.Id, "UserDeactivated", cancellationToken);
        }

        dbContext.AuditLogs.Add(AuditLog.Create(
            timeProvider.GetUtcNow().UtcDateTime,
            isActive ? "UserActivated" : "UserDeactivated",
            "ApplicationUser",
            actorUserId,
            target.Id.ToString(),
            $"Account active state changed to {isActive}.",
            ipAddress));
        await dbContext.SaveChangesAsync(cancellationToken);
        return UserManagementOutcome.Succeeded;
    }, cancellationToken);

    private async Task<bool> HasOtherActiveAdminAsync(Guid excludedUserId)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        return admins.Any(admin => admin.Id != excludedUserId && admin.IsActive);
    }

    private async Task RevokeRefreshSessionsAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now, reason);
        }
    }

    private async Task TryDeleteProfileImageAsync(
        string publicId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await profileImageStorage.DeleteAsync(publicId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not clean up Cloudinary profile image for user {UserId}.", userId);
        }
    }

    private async Task<T> ExecuteTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            var codes = string.Join(",", result.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"{message} Identity error codes: {codes}");
        }
    }
}
