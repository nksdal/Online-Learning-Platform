namespace OnlineLearningPlatform.Application.Common.Exceptions;

/// <summary>The caller is authenticated but not allowed to do this (ownership, role, enrollment). Maps to 403.</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
}
