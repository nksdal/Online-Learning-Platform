using FluentValidation;

namespace OnlineLearningPlatform.Application.Authentication;

public sealed record RegisterRequest(string Email, string FirstName, string LastName, string Password, string? TimeZoneId = null);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(10)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain a digit.")
            .Must(password => password.Distinct().Count() >= 4)
            .WithMessage("Password must contain at least four different characters.");
        RuleFor(request => request.TimeZoneId).MaximumLength(64);
    }
}

public sealed record VerifyEmailRequest(Guid UserId, string Token);

public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4096);
    }
}

public sealed record ResendVerificationRequest(string Email);

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed record RequestEmailChangeRequest(string NewEmail, string CurrentPassword);

public sealed class RequestEmailChangeRequestValidator : AbstractValidator<RequestEmailChangeRequest>
{
    public RequestEmailChangeRequestValidator()
    {
        RuleFor(request => request.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(256);
    }
}

public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token);

public sealed class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4096);
    }
}

public enum EmailChangeRequestOutcome
{
    ConfirmationSent,
    UserNotFound,
    InvalidPassword,
    EmailInUse,
    SameEmail,
}

public enum EmailChangeConfirmationOutcome
{
    Succeeded,
    InvalidOrExpired,
    EmailInUse,
}

public sealed record ForgotPasswordRequest(string Email);

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4096);
        RuleFor(request => request.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(256)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain a digit.")
            .Must(password => password.Distinct().Count() >= 4)
            .WithMessage("Password must contain at least four different characters.");
    }
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(256);
        RuleFor(request => request.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(256)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain a digit.")
            .Must(password => password.Distinct().Count() >= 4)
            .WithMessage("Password must contain at least four different characters.");
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(256);
    }
}

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);

    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken);

    Task<EmailChangeRequestOutcome> RequestEmailChangeAsync(
        Guid userId,
        RequestEmailChangeRequest request,
        CancellationToken cancellationToken);

    Task<EmailChangeConfirmationOutcome> ConfirmEmailChangeAsync(
        ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken);

    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<AccessTokenResponse?> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<AuthenticatedSession?> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task<AuthenticatedSession?> RefreshAsync(string? refreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken);

    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);

    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

public sealed record AuthMessageResponse(string Message);

public sealed record AccessTokenResponse(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Internal result used by the API to set the HttpOnly refresh cookie. Never serialize the raw refresh token.</summary>
public sealed record AuthenticatedSession(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
