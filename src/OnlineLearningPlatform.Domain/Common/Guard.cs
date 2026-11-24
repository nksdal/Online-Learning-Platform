namespace OnlineLearningPlatform.Domain.Common;

/// <summary>Small argument checks shared by domain entities.</summary>
internal static class Guard
{
    /// <summary>
    /// Enforces the project-wide time policy: every timestamp is UTC and says so explicitly.
    /// A <see cref="DateTimeKind.Unspecified"/> value is rejected because nobody can tell which zone it is in.
    /// </summary>
    public static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamps must have DateTimeKind.Utc.", paramName);
        }
    }

    public static void EnsureMaxLength(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"Value must be at most {maxLength} characters.", paramName);
        }
    }

    /// <summary>For untrusted client metadata (user agent, IP): shorten rather than fail the whole request.</summary>
    public static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
