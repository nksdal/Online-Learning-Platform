using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace OnlineLearningPlatform.Api.Validation;

/// <summary>
/// Runs the FluentValidation validator (if one is registered) for every action argument before the
/// action executes. Failures are added to ModelState and returned through ASP.NET Core's standard
/// invalid-model response, so FluentValidation errors and built-in model-binding errors share ONE shape:
/// a 400 ValidationProblemDetails whose "errors" keys are camelCase (matching the JSON the client sent).
/// Controllers therefore contain no validation code.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            foreach (var failure in result.Errors)
            {
                context.ModelState.AddModelError(ToCamelCasePath(failure.PropertyName), failure.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            var apiBehavior = services.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
            context.Result = apiBehavior.InvalidModelStateResponseFactory(context);
            return;
        }

        await next();
    }

    // "Address.PostalCode" -> "address.postalCode"
    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
