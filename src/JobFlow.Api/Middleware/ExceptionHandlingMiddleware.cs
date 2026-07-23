using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JobFlow.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        if (exception is FluentValidation.ValidationException validationEx)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var response = new
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Validation failed.",
                Errors = validationEx.Errors.Select(e => e.ErrorMessage).ToArray()
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }

        var statusCode = exception switch
        {
            System.Collections.Generic.KeyNotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        var errorResponse = new
        {
            StatusCode = context.Response.StatusCode,
            Message = statusCode == HttpStatusCode.InternalServerError
                ? "An internal error occurred."
                : exception.Message
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
}
