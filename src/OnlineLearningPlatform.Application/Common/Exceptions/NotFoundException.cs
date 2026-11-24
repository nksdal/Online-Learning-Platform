namespace OnlineLearningPlatform.Application.Common.Exceptions;

/// <summary>The resource does not exist, or the caller is not allowed to know that it exists. Maps to 404.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
}
