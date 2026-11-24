namespace OnlineLearningPlatform.Application.Common.Exceptions;

/// <summary>
/// The request is well-formed but breaks a business rule (enrolling in an unpublished course,
/// submitting after the deadline, an invalid course status transition). Maps to 422.
/// </summary>
public sealed class BusinessRuleException(string message) : AppException(message)
{
}
