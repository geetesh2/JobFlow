using System.Security.Claims;
using JobFlow.Api.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JobFlow.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/identity/me", (ClaimsPrincipal user) =>
        {
            var roles = user.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role);

            return Results.Ok(new
            {
                Subject = user.FindFirst("sub")?.Value,
                Username = user.Identity?.Name,
                Roles = roles
            });
        })
        .RequireAuthorization(JobFlowPolicies.UserAccess)
        .WithName("GetCurrentIdentity");

        return endpoints;
    }
}
