using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OnlineLearningPlatform.Infrastructure.Persistence.Converters;

/// <summary>
/// SQL Server's datetime2 has no time-zone or "kind" information, so EF Core returns values with
/// DateTimeKind.Unspecified. Everything we store is UTC, so we label values as UTC when reading them back.
/// Writing is unchanged: the domain already rejects non-UTC timestamps.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
