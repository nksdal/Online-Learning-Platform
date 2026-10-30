using Microsoft.EntityFrameworkCore;
using OnlineLearningPlatform.Domain.Auditing;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.Application.Abstractions;

/// <summary>
/// What the Application layer needs from the database. Implemented by the EF Core DbContext in
/// Infrastructure. Application services query these sets directly with LINQ; there is no
/// repository layer on top because DbContext already is a unit of work.
/// New DbSets are added here as new entities arrive.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
