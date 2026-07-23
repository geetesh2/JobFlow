using JobFlow.Application.Abstractions.Services;
using Microsoft.AspNetCore.Http;

namespace JobFlow.Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        if (context.Request.Headers.TryGetValue("Idempotency-Key", out var key))
        {
            var idempotencyKey = $"idempotency:{key}";

            if (!await idempotencyService.TryAcquireKeyAsync(idempotencyKey, TimeSpan.FromMinutes(10)))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsync("Request with this Idempotency-Key is currently being processed.");
                return;
            }

            try
            {
                await _next(context);
            }
            finally
            {
                // In a real scenario, you'd only release if successful, or implement state management
                // to distinguish between 'processing' and 'completed'.
                await idempotencyService.ReleaseKeyAsync(idempotencyKey);
            }
        }
        else
        {
            await _next(context);
        }
    }
}
