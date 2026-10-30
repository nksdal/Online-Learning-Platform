using Microsoft.AspNetCore.DataProtection;
using OnlineLearningPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings:DefaultConnection " +
        "(user-secrets, appsettings.Development.json) or the ConnectionStrings__DefaultConnection environment variable.");

builder.Services.AddInfrastructure(connectionString);

// Identity protects email-confirmation and password-reset tokens with Data Protection keys.
// The key ring must be persisted and shared before deployment, or every restart invalidates
// outstanding tokens (tracked in docs/checklist.md, Week 4).
builder.Services.AddDataProtection().SetApplicationName("OnlineLearningPlatform");

var app = builder.Build();

app.MapGet("/", () => Results.Text("OnlineLearningPlatform API - foundation only. Swagger, health checks and authentication arrive in the next steps."));

app.Run();

// Makes the entry point visible to integration tests (WebApplicationFactory<Program>).
public partial class Program { }
