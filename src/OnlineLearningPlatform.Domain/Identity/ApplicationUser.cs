using Microsoft.AspNetCore.Identity;
using OnlineLearningPlatform.Domain.Common;

namespace OnlineLearningPlatform.Domain.Identity;

/// <summary>
/// The platform user. ASP.NET Core Identity supplies email, password hash, lockout counters,
/// security stamp and concurrency stamp; this class adds only what the platform itself needs.
/// There is deliberately no role property: the role lives in Identity's role tables.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditableEntity
{
    public const int NameMaxLength = 100;
    public const int TimeZoneIdMaxLength = 64;
    public const string DefaultTimeZoneId = "UTC";

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>IANA time-zone id (for example "Asia/Tbilisi"). Decides what a "learning day" is.</summary>
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    /// <summary>False = suspended. Suspended users are rejected even if they hold a valid access token.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set for the seeded administrator until the initial password has been changed.</summary>
    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
