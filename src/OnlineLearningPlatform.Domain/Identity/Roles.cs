namespace OnlineLearningPlatform.Domain.Identity;

/// <summary>The three roles of the platform. Each user has exactly one in v1.</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Student, Teacher, Admin];
}
