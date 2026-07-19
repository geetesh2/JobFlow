using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace JobFlow.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddJobFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global Fixed Window Limiter: 100 requests per minute per IP
            options.AddFixedWindowLimiter("global", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 100;
                opt.QueueLimit = 0;
            });

            // Job Submission Limiter: 5 requests per 10 seconds per user (using identity)
            options.AddPolicy("job-submission", context =>
            {
                var user = context.User.Identity?.Name ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(user, _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromSeconds(10),
                    PermitLimit = 5,
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}
