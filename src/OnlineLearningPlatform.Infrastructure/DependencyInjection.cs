using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Application.Catalog;
using OnlineLearningPlatform.Application.Authentication;
using OnlineLearningPlatform.Infrastructure.Authentication;
using OnlineLearningPlatform.Infrastructure.Catalog;
using OnlineLearningPlatform.Infrastructure.Email;
using OnlineLearningPlatform.Domain.Identity;
using OnlineLearningPlatform.Infrastructure.Health;
using OnlineLearningPlatform.Infrastructure.Storage;
using OnlineLearningPlatform.Infrastructure.Persistence;

namespace OnlineLearningPlatform.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers everything Infrastructure provides. Called once from the Api's Program.cs.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The clock is injected, never read from DateTime.UtcNow, so tests can control time.
        services.TryAddSingleton(TimeProvider.System);

        // The connection string is read when the DbContext is first created, not while services are being
        // registered. That keeps startup order-independent and lets integration tests override configuration.
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            }

            // Retries transient SQL errors. Note: explicit transactions must run inside
            // dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...) when this is enabled.
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
        });
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddHttpClient<IProfileImageStorage, CloudinaryProfileImageStorage>();
        services.AddHttpClient<ICourseImageStorage, CloudinaryCourseImageStorage>();
        services.AddHttpClient<IMediaAssetStorage, CloudinaryMediaAssetStorage>(client =>
            client.Timeout = TimeSpan.FromMinutes(10));
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
        services.AddScoped<IDiscussionService, DiscussionService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IAssignmentDeadlineReminderProcessor, AssignmentDeadlineReminderProcessor>();
        services.AddHostedService<BootstrapAdminSeeder>();
        services.AddHostedService<DemoCatalogSeeder>();
        services.AddHostedService<AssignmentDeadlineReminderWorker>();

        services.AddOptions<AssignmentDeadlineReminderOptions>()
            .BindConfiguration(AssignmentDeadlineReminderOptions.SectionName)
            .Validate(options => options.PollIntervalMinutes is >= 1 and <= 1440,
                "Assignment deadline reminder poll interval must be between 1 and 1440 minutes.")
            .Validate(options => options.ReminderWindowHours is >= 1 and <= 168,
                "Assignment deadline reminder window must be between 1 and 168 hours.")
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .Validate(options => System.Net.Mail.MailAddress.TryCreate(options.From, out _), "Email sender address is invalid.")
            .Validate(options => Uri.TryCreate(options.AppUrl, UriKind.Absolute, out var uri) &&
                                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                                 string.IsNullOrEmpty(uri.UserInfo),
                "Email app URL must be an absolute HTTP or HTTPS URL without user information.")
            .ValidateOnStart();
        services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Email SMTP host is required.")
            .Validate(options => options.Port is > 0 and <= 65535, "Email SMTP port is invalid.");
        services.AddOptions<CloudinaryOptions>()
            .BindConfiguration(CloudinaryOptions.SectionName);
        services.TryAddTransient<IEmailSender, GmailSmtpEmailSender>();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(3));

        // AddIdentityCore (not AddIdentity): we want the user/role/token machinery but NOT Identity's
        // cookie authentication, because this API authenticates with JWT bearer tokens.
        // Note: we do not opt into Identity schema version 3, so no passkey table is created.
        services
            .AddIdentityCore<ApplicationUser>(ConfigureIdentity)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // "ready" = the app can serve real requests. /health/live deliberately runs no checks.
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;

        // Email must be confirmed before login. (The login service enforces this explicitly as well.)
        options.SignIn.RequireConfirmedEmail = true;

        // Length matters more than character-class rules, so: 10+ characters, mixed case and a digit.
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 4;

        // 5 wrong passwords lock the account for 15 minutes.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }
}
