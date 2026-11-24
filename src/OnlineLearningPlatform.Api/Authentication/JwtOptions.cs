namespace OnlineLearningPlatform.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException($"Configuration '{SectionName}:Issuer' is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException($"Configuration '{SectionName}:Audience' is required.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:SigningKey' must contain at least 32 UTF-8 bytes.");
        }

        if (AccessTokenMinutes is < 1 or > 15)
        {
            throw new InvalidOperationException($"Configuration '{SectionName}:AccessTokenMinutes' must be between 1 and 15.");
        }
    }
}

public sealed class JwtOptionsValidator : Microsoft.Extensions.Options.IValidateOptions<JwtOptions>
{
    public Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        try
        {
            options.Validate();
            return Microsoft.Extensions.Options.ValidateOptionsResult.Success;
        }
        catch (InvalidOperationException exception)
        {
            return Microsoft.Extensions.Options.ValidateOptionsResult.Fail(exception.Message);
        }
    }
}
