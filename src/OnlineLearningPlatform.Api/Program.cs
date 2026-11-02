using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Api.Documentation;
using OnlineLearningPlatform.Api.ErrorHandling;
using OnlineLearningPlatform.Api.Validation;
using OnlineLearningPlatform.Application.Abstractions;
using OnlineLearningPlatform.Infrastructure;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: Serilog, configured from the "Serilog" section of appsettings --------------------
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---- Services ----------------------------------------------------------------------------------
builder.Services.AddInfrastructure();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtSettings) =>
    {
        var jwt = jwtSettings.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(OnlineLearningPlatform.Domain.Identity.Roles.Admin))
    .AddPolicy(AuthorizationPolicies.Teacher, policy => policy.RequireRole(OnlineLearningPlatform.Domain.Identity.Roles.Teacher))
    .AddPolicy(AuthorizationPolicies.Student, policy => policy.RequireRole(OnlineLearningPlatform.Domain.Identity.Roles.Student));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimitPolicy.Name, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
    .WithHeaders("Content-Type", "Authorization", "X-Refresh-Request")
    .AllowCredentials()));

// Identity protects email-confirmation and password-reset tokens with Data Protection keys.
// The key ring must be persisted and shared before deployment, or every restart invalidates
// outstanding tokens (tracked in docs/checklist.md, Week 4).
builder.Services.AddDataProtection().SetApplicationName("OnlineLearningPlatform");

// Validators live in the Application layer; the API only runs them (see ValidationFilter).
builder.Services.AddValidatorsFromAssemblyContaining<IApplicationDbContext>();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Errors: every non-success response is an RFC 9457 Problem Details document.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// Configuration is read AFTER Build() so that every configuration source (including test overrides) is included.
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings:DefaultConnection " +
        "(user-secrets, appsettings.Development.json) or the ConnectionStrings__DefaultConnection environment variable.");
}

var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");

// ---- Pipeline (order matters: first = outermost) ------------------------------------------------
// Request logging sits outside the exception handler so it records the final status code (500 for a
// crash) while the handler logs the exception itself exactly once. Only the path is logged, never the
// query string, so tokens in URLs cannot reach the logs.
app.UseSerilogRequestLogging(options =>
{
    // Docker and load balancers probe /health every few seconds; keep that noise out of the normal log level.
    options.GetLevel = (httpContext, _, exception) =>
        httpContext.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
        : LogEventLevel.Information;
});

app.UseExceptionHandler();   // uncaught exceptions -> GlobalExceptionHandler -> Problem Details
app.UseStatusCodePages();    // bare 404/405/... responses -> Problem Details
app.UseCors("Frontend");     // credentialed cookies only for explicitly allowed frontend origins
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// Liveness: "the process is up". Runs no checks, so a database outage never makes an orchestrator kill a healthy app.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

// Readiness: "the app can serve requests" = every check tagged "ready" (database). 503 when it cannot.
// The response body is just "Healthy"/"Unhealthy": no internal detail is exposed.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.Run();

// Makes the entry point visible to integration tests (WebApplicationFactory<Program>).
public partial class Program { }
