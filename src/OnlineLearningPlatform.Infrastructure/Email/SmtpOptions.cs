namespace OnlineLearningPlatform.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 465;

    public bool Secure { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }
}
