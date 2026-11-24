namespace OnlineLearningPlatform.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = "your-account@gmail.com";

    /// <summary>The Angular frontend origin used to build verification and password-reset links.</summary>
    public string AppUrl { get; set; } = "http://localhost:4200";

    public string VerificationPageUrl => $"{AppUrl.TrimEnd('/')}/verify-email";

    public string PasswordResetPageUrl => $"{AppUrl.TrimEnd('/')}/reset-password";
}
