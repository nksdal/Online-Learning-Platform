namespace OnlineLearningPlatform.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string Admin = "AdminOnly";
    public const string Teacher = "TeacherOnly";
    public const string Student = "StudentOnly";
}
