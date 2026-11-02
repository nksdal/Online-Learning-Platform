using FluentValidation;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Application.Authentication;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string TimeZoneId,
    IReadOnlyCollection<string> Roles);

public sealed record AdminUserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    bool MustChangePassword,
    IReadOnlyCollection<string> Roles);

public sealed record AdminUserListQuery(int Page = 1, int PageSize = 50, string? Search = null);

public sealed class AdminUserListQueryValidator : AbstractValidator<AdminUserListQuery>
{
    public AdminUserListQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(100);
    }
}

public sealed record PagedAdminUsersResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<AdminUserSummary> Users);

public sealed record ChangeUserRoleRequest(string Role);

public sealed class ChangeUserRoleRequestValidator : AbstractValidator<ChangeUserRoleRequest>
{
    public ChangeUserRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(role => Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be Student, Teacher, or Admin.");
    }
}

public sealed record ChangeUserStatusRequest(bool IsActive);

public enum UserManagementOutcome
{
    Succeeded,
    NotFound,
    LastActiveAdmin,
}

public interface IUserManagementService
{
    Task<UserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedAdminUsersResponse> ListUsersAsync(AdminUserListQuery query, CancellationToken cancellationToken);

    Task<UserManagementOutcome> ChangeRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        string role,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<UserManagementOutcome> ChangeStatusAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isActive,
        string? ipAddress,
        CancellationToken cancellationToken);
}
