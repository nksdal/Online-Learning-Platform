using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Email;

namespace OnlineLearningPlatform.Infrastructure.Authentication;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    SignInManager<ApplicationUser> signInManager,
    IAccessTokenService accessTokenService,
    Persistence.ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<EmailOptions> emailOptions,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(request.Email);
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                ? ApplicationUser.DefaultTimeZoneId
                : request.TimeZoneId.Trim(),
            IsActive = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return;
            }

            var errorCodes = string.Join(",", createResult.Errors.Select(error => error.Code));
            logger.LogError("Identity rejected a registration after request validation. Error codes: {ErrorCodes}", errorCodes);
            throw new InvalidOperationException("The account could not be created.");
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Student);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            var errorCodes = string.Join(",", roleResult.Errors.Select(error => error.Code));
            logger.LogError("Could not assign the default Student role. Error codes: {ErrorCodes}", errorCodes);
            throw new InvalidOperationException("The account could not be created.");
        }

        await SendVerificationEmailAsync(user, cancellationToken);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsActionTokenFormatValid(request.Token))
        {
            return;
        }

        var tokenHash = HashActionToken(request.Token);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        var verified = await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var user = await userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null || user.EmailConfirmed || !user.IsActive || user.IsDeleted)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var actionToken = await dbContext.AccountActionTokens.SingleOrDefaultAsync(
                token => token.UserId == user.Id &&
                         token.Purpose == AccountActionTokenPurpose.EmailVerification &&
                         token.TokenHash == tokenHash,
                cancellationToken);
            if (actionToken is null || !actionToken.IsUsable(now))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            actionToken.MarkUsed(now);
            user.EmailConfirmed = true;
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                var errorCodes = string.Join(",", stampResult.Errors.Select(error => error.Code));
                logger.LogError("Could not confirm an email address securely. Identity error codes: {ErrorCodes}", errorCodes);
                throw new InvalidOperationException("The email address could not be confirmed securely.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });

        if (!verified)
        {
            logger.LogInformation("An email verification attempt was rejected.");
        }
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(userManager.NormalizeEmail(request.Email));
        if (user is null || user.EmailConfirmed || !user.IsActive)
        {
            return;
        }

        await SendVerificationEmailAsync(user, cancellationToken);
    }

    public async Task<EmailChangeRequestOutcome> RequestEmailChangeAsync(
        Guid userId,
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return EmailChangeRequestOutcome.UserNotFound;
        }

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return EmailChangeRequestOutcome.InvalidPassword;
        }

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return EmailChangeRequestOutcome.SameEmail;
        }

        var existingEmailUser = await userManager.FindByEmailAsync(newEmail);
        var existingUserName = await userManager.FindByNameAsync(newEmail);
        if ((existingEmailUser is not null && existingEmailUser.Id != user.Id) ||
            (existingUserName is not null && existingUserName.Id != user.Id))
        {
            return EmailChangeRequestOutcome.EmailInUse;
        }

        var identityToken = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken));
        var confirmationUrl = QueryHelpers.AddQueryString(
            emailOptions.Value.VerificationPageUrl,
            new Dictionary<string, string?>
            {
                ["emailChange"] = "1",
                ["userId"] = user.Id.ToString(),
                ["newEmail"] = newEmail,
                ["token"] = encodedToken,
            });

        await emailSender.SendAsync(
            new EmailMessage(
                newEmail,
                "Confirm your new Nikladori email address",
                $"Confirm your new email address by opening this link: {confirmationUrl}",
                $"<p>Confirm this email address for your Nikladori account.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(confirmationUrl)}\">Confirm new email address</a></p><p>If you did not request this, you can ignore this message.</p>"),
            cancellationToken);

        return EmailChangeRequestOutcome.ConfirmationSent;
    }

    public async Task<EmailChangeConfirmationOutcome> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string identityToken;
        try
        {
            identityToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return EmailChangeConfirmationOutcome.InvalidOrExpired;
        }

        var newEmail = request.NewEmail.Trim();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = await userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null || !user.IsActive)
            {
                return EmailChangeConfirmationOutcome.InvalidOrExpired;
            }

            var existingEmailUser = await userManager.FindByEmailAsync(newEmail);
            var updateUserName = string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase);
            var existingUserName = updateUserName ? await userManager.FindByNameAsync(newEmail) : null;
            if ((existingEmailUser is not null && existingEmailUser.Id != user.Id) ||
                (existingUserName is not null && existingUserName.Id != user.Id))
            {
                return EmailChangeConfirmationOutcome.EmailInUse;
            }

            var changeResult = await userManager.ChangeEmailAsync(user, newEmail, identityToken);
            if (!changeResult.Succeeded)
            {
                if (changeResult.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                {
                    return EmailChangeConfirmationOutcome.EmailInUse;
                }

                logger.LogInformation("An email-change confirmation was rejected. Identity error codes: {ErrorCodes}",
                    string.Join(",", changeResult.Errors.Select(error => error.Code)));
                return EmailChangeConfirmationOutcome.InvalidOrExpired;
            }

            if (updateUserName)
            {
                var userNameResult = await userManager.SetUserNameAsync(user, newEmail);
                if (!userNameResult.Succeeded)
                {
                    if (userNameResult.Errors.Any(error => error.Code == "DuplicateUserName"))
                    {
                        return EmailChangeConfirmationOutcome.EmailInUse;
                    }

                    var errorCodes = string.Join(",", userNameResult.Errors.Select(error => error.Code));
                    logger.LogError("Could not update a username after email confirmation. Identity error codes: {ErrorCodes}", errorCodes);
                    throw new InvalidOperationException("The account sign-in name could not be updated.");
                }
            }

            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                var errorCodes = string.Join(",", stampResult.Errors.Select(error => error.Code));
                logger.LogError("Could not refresh the security stamp after an email change. Identity error codes: {ErrorCodes}", errorCodes);
                throw new InvalidOperationException("The email address could not be changed securely.");
            }

            await RevokeUserSessionsAsync(user.Id, timeProvider.GetUtcNow().UtcDateTime, "EmailChanged", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return EmailChangeConfirmationOutcome.Succeeded;
        });
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(userManager.NormalizeEmail(request.Email));
        if (user is null || !user.IsActive || !user.EmailConfirmed)
        {
            return;
        }

        var token = await IssueActionTokenAsync(
            user.Id,
            AccountActionTokenPurpose.PasswordReset,
            TimeSpan.FromMinutes(30),
            cancellationToken);
        var resetUrl = QueryHelpers.AddQueryString(
            emailOptions.Value.PasswordResetPageUrl,
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id.ToString(),
                ["token"] = token,
            });

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    user.Email!,
                    "Reset your Online Learning Platform password",
                    $"Reset your password by opening this link: {resetUrl} This link expires in 30 minutes. If you did not request this, you can ignore this message.",
                    $"<p>A password reset was requested for your account.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(resetUrl)}\">Reset password</a></p><p>This link expires in 30 minutes. If you did not request this, you can ignore this message.</p>"),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Keep the forgot-password response identical for known and unknown email addresses.
            logger.LogWarning("A password recovery email could not be delivered.");
        }
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsActionTokenFormatValid(request.Token))
        {
            return false;
        }

        var tokenHash = HashActionToken(request.Token);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var user = await userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null || !user.IsActive || user.IsDeleted || !user.EmailConfirmed)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var actionToken = await dbContext.AccountActionTokens.SingleOrDefaultAsync(
                token => token.UserId == user.Id &&
                         token.Purpose == AccountActionTokenPurpose.PasswordReset &&
                         token.TokenHash == tokenHash,
                cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (actionToken is null || !actionToken.IsUsable(now))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var validationErrors = new List<IdentityError>();
            foreach (var passwordValidator in userManager.PasswordValidators)
            {
                var validation = await passwordValidator.ValidateAsync(userManager, user, request.NewPassword);
                if (!validation.Succeeded)
                {
                    validationErrors.AddRange(validation.Errors);
                }
            }

            if (validationErrors.Count > 0)
            {
                logger.LogInformation("A password reset was rejected by Identity policy. Error codes: {ErrorCodes}",
                    string.Join(",", validationErrors.Select(error => error.Code)));
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            actionToken.MarkUsed(now);
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.NewPassword);
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                var errorCodes = string.Join(",", stampResult.Errors.Select(error => error.Code));
                logger.LogError("Could not securely complete a password reset. Identity error codes: {ErrorCodes}", errorCodes);
                throw new InvalidOperationException("The password could not be reset securely.");
            }

            var activeRefreshTokens = await dbContext.RefreshTokens
                .Where(token => token.UserId == user.Id && token.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var refreshToken in activeRefreshTokens)
            {
                refreshToken.Revoke(now, "PasswordReset");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<AccessTokenResponse?> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
        {
            return null;
        }

        user.MustChangePassword = false;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errorCodes = string.Join(",", updateResult.Errors.Select(error => error.Code));
            logger.LogError("Could not save the changed-password flag. Identity error codes: {ErrorCodes}", errorCodes);
            throw new InvalidOperationException("The password was changed, but the account update could not be completed.");
        }

        await RevokeUserSessionsAsync(user.Id, timeProvider.GetUtcNow().UtcDateTime, "PasswordChanged", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var accessToken = accessTokenService.Create(
            user.Id,
            user.Email!,
            roles.ToArray(),
            mustChangePassword: false,
            securityStamp);
        return new AccessTokenResponse(accessToken.Value, accessToken.ExpiresAtUtc);
    }

    public async Task<AuthenticatedSession?> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(userManager.NormalizeEmail(request.Email));
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return await CreateSessionAsync(user, roles.ToArray(), ipAddress, userAgent, cancellationToken);
    }

    public async Task<AuthenticatedSession?> RefreshAsync(
        string? refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // A retry starts from a clean unit of work. The serializable transaction ensures two
            // requests cannot both rotate the same token successfully on SQL Server.
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash, cancellationToken);
            if (token is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (token.IsRevoked)
            {
                await RevokeFamilyAsync(token.FamilyId, now, "ReuseDetected", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            if (token.IsExpired(now))
            {
                token.Revoke(now, "Expired");
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var user = await userManager.FindByIdAsync(token.UserId.ToString());
            if (user is null || !user.IsActive || !user.EmailConfirmed)
            {
                await RevokeFamilyAsync(token.FamilyId, now, "AccountUnavailable", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var roles = await userManager.GetRolesAsync(user);
            var replacement = CreateRefreshToken(user.Id, token.FamilyId, now, ipAddress, userAgent);
            token.Revoke(now, "Rotated", replacement.Entity.Id);
            await dbContext.RefreshTokens.AddAsync(replacement.Entity, cancellationToken);

            var securityStamp = await userManager.GetSecurityStampAsync(user);
            var accessToken = accessTokenService.Create(
                user.Id, user.Email!, roles.ToArray(), user.MustChangePassword, securityStamp);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AuthenticatedSession(
                accessToken.Value,
                accessToken.ExpiresAtUtc,
                replacement.RawToken,
                replacement.Entity.ExpiresAtUtc);
        });
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            item => item.TokenHash == tokenHash, cancellationToken);
        if (token is null)
        {
            return;
        }

        await RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow().UtcDateTime, "Logout", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now, "LogoutAll");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticatedSession> CreateSessionAsync(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var familyId = Guid.NewGuid();
        var refresh = CreateRefreshToken(user.Id, familyId, now, ipAddress, userAgent);
        await dbContext.RefreshTokens.AddAsync(refresh.Entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var securityStamp = await userManager.GetSecurityStampAsync(user);
        var accessToken = accessTokenService.Create(
            user.Id, user.Email!, roles.ToArray(), user.MustChangePassword, securityStamp);
        return new AuthenticatedSession(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refresh.RawToken,
            refresh.Entity.ExpiresAtUtc);
    }

    private (RefreshToken Entity, string RawToken) CreateRefreshToken(
        Guid userId,
        Guid familyId,
        DateTime nowUtc,
        string? ipAddress,
        string? userAgent)
    {
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var entity = RefreshToken.Create(
            userId,
            familyId,
            HashRefreshToken(rawToken),
            nowUtc,
            TimeSpan.FromDays(30),
            ipAddress,
            userAgent);
        return (entity, rawToken);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(item => item.FamilyId == familyId && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(nowUtc, reason);
        }
    }

    private async Task RevokeUserSessionsAsync(
        Guid userId,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(nowUtc, reason);
        }
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task SendVerificationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = await IssueActionTokenAsync(
            user.Id,
            AccountActionTokenPurpose.EmailVerification,
            TimeSpan.FromHours(24),
            cancellationToken);
        var verificationUrl = QueryHelpers.AddQueryString(
            emailOptions.Value.VerificationPageUrl,
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id.ToString(),
                ["token"] = token,
            });

        var message = new EmailMessage(
            user.Email!,
            "Verify your Online Learning Platform account",
            $"Verify your email by opening this link: {verificationUrl} This link expires in 24 hours.",
            $"<p>Verify your email address to activate your account.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(verificationUrl)}\">Verify email</a></p><p>This link expires in 24 hours.</p>");

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Keep registration and resend responses generic even when the provider is unavailable.
            logger.LogWarning("An account verification email could not be delivered.");
        }
    }

    private async Task<string> IssueActionTokenAsync(
        Guid userId,
        AccountActionTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var tokenHash = HashActionToken(rawToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            var outstandingTokens = await dbContext.AccountActionTokens
                .Where(token => token.UserId == userId && token.Purpose == purpose && token.UsedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var outstandingToken in outstandingTokens)
            {
                outstandingToken.MarkUsed(now);
            }

            dbContext.AccountActionTokens.Add(
                AccountActionToken.Create(userId, tokenHash, purpose, now, lifetime));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return rawToken;
    }

    private static bool IsActionTokenFormatValid(string token) =>
        token.Length == AccountActionToken.TokenHashMaxLength && token.All(Uri.IsHexDigit);

    private static string HashActionToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
