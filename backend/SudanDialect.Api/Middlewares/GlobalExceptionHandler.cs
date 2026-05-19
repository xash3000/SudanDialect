using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SudanDialect.Api.Middlewares;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        if (exception is ArgumentOutOfRangeException argumentOutOfRangeException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Bad Request";
            problemDetails.Detail = argumentOutOfRangeException.Message;
            problemDetails.Extensions["error"] = argumentOutOfRangeException.Message;
        }
        else if (exception is ArgumentException argumentException)
        {
            problemDetails.Status = StatusCodes.Status400BadRequest;
            problemDetails.Title = "Bad Request";
            problemDetails.Detail = argumentException.Message;
            problemDetails.Extensions["error"] = argumentException.Message;
        }
        else if (exception is InvalidOperationException invalidOperationException)
        {
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            problemDetails.Title = "Internal Server Error";
            problemDetails.Detail = invalidOperationException.Message;
            problemDetails.Extensions["error"] = invalidOperationException.Message;
        }
        else
        {
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            problemDetails.Title = "Internal Server Error";
            problemDetails.Detail = "An unexpected error occurred.";
            problemDetails.Extensions["error"] = "An unexpected error occurred.";
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
