namespace OnlineLearningPlatform.Application.Common.Exceptions;

/// <summary>The request conflicts with current state (already enrolled, duplicate, stale update). Maps to 409.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
}
