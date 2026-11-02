namespace OnlineLearningPlatform.Application.Common.Exceptions;

/// <summary>
/// Base class for expected, business-level failures. The API maps each subclass to an HTTP status
/// and returns <see cref="Exception.Message"/> to the client, so a message MUST be safe to show to
/// any user: never put emails, ids of other users, SQL, paths or tokens in it.
/// Every other exception type is treated as a bug and is reported to the client without detail.
/// </summary>
public abstract class AppException(string message) : Exception(message)
{
}
