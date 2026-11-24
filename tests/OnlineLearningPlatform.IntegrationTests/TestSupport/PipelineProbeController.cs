using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OnlineLearningPlatform.Api.Authentication;
using OnlineLearningPlatform.Application.Common.Exceptions;
using OnlineLearningPlatform.Domain.Identity;

namespace OnlineLearningPlatform.IntegrationTests.TestSupport;

/// <summary>
/// Test-only endpoints. They exist so the cross-cutting pipeline (error mapping, validation) can be
/// tested end to end before real feature controllers exist. Never part of the production API.
/// </summary>
[ApiController]
[Route("test-support")]
public class PipelineProbeController : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() => throw new NotFoundException("The requested course was not found.");

    [HttpGet("forbidden")]
    public IActionResult ThrowForbidden() => throw new ForbiddenException("You do not manage this course.");

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() => throw new ConflictException("You are already enrolled in this course.");

    [HttpGet("business-rule")]
    public IActionResult ThrowBusinessRule() => throw new BusinessRuleException("Only published courses accept enrollments.");

    [HttpGet("crash")]
    public IActionResult Crash() => throw new InvalidOperationException("do-not-leak: Server=prod-db;Password=hunter2");

    [HttpPost("validate")]
    public IActionResult Validate(ProbeRequest request) => Ok(new { accepted = request.Email });

    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected() => Ok();

    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [HttpGet("admin")]
    public IActionResult AdminOnly() => Ok();
}

public sealed record ProbeRequest(string? Email, int Age);

public sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
{
    public ProbeRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Age).InclusiveBetween(13, 120);
    }
}
