namespace OnlineLearningPlatform.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@online-learning-platform.local";

    public string FromName { get; set; } = "Online Learning Platform";

    public string VerificationPageUrl { get; set; } = "http://localhost:4200/verify-email";

    public string PasswordResetPageUrl { get; set; } = "http://localhost:4200/reset-password";
}
