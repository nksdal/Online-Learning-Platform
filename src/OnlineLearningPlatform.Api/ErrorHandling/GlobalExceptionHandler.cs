using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Application.Common.Exceptions;

namespace OnlineLearningPlatform.Api.ErrorHandling;

/// <summary>
/// The single place where exceptions become HTTP responses (RFC 9457 Problem Details).
/// Expected business failures (<see cref="AppException"/> subclasses) map to 4xx and show their message.
/// Anything else is a bug: it is logged in full on the server and the client only receives a generic
/// 500 with a trace id, so no stack trace, SQL or configuration detail can leak.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected. Nobody is waiting for a response and this is not a server fault.
            return true;
        }

        var (status, title, detail) = Describe(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with {StatusCode} ({ExceptionType})", status, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }

    private static (int Status, string Title, string Detail) Describe(Exception exception) => exception switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
        ForbiddenException e => (StatusCodes.Status403Forbidden, "Forbidden", e.Message),
        ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
        BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violation", e.Message),
        _ => (
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred",
            "The server could not process the request. Please try again later."),
    };
}
