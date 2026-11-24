using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Api.Authentication;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly PathString ChangePasswordPath = new("/api/users/me/password");
    private static readonly PathString LogoutPath = new("/api/auth/logout");

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsLogout(context.Request.Method, context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "The access token subject is invalid.");
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        var tokenStamp = context.User.FindFirst("security_stamp")?.Value;
        if (user is null || !user.IsActive || string.IsNullOrEmpty(tokenStamp) ||
            !string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "The account is inactive or the access token is no longer valid.");
            return;
        }

        if (user.MustChangePassword && !IsAllowedWhilePasswordChangeIsRequired(context.Request.Method, context.Request.Path))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Password change required.");
            return;
        }

        await next(context);
    }

    private static bool IsAllowedWhilePasswordChangeIsRequired(string method, PathString path) =>
        HttpMethods.IsPost(method) && (path == ChangePasswordPath || path == LogoutPath);

    private static bool IsLogout(string method, PathString path) =>
        HttpMethods.IsPost(method) && path == LogoutPath;

    private static async Task WriteProblemAsync(HttpContext context, int status, string title)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = status == StatusCodes.Status401Unauthorized
                ? "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                : "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            Title = title,
            Status = status,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await context.Response.WriteAsync(json, context.RequestAborted);
    }
}
