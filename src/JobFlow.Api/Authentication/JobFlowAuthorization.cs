using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace JobFlow.Api.Authentication;

public static class JobFlowRoles
{
    public const string User = "jobflow-user";
    public const string Admin = "jobflow-admin";
}

public static class JobFlowPolicies
{
    public const string UserAccess = "JobFlowUserAccess";
}

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication authority was not configured.");
        var audience = configuration["Authentication:Audience"]
            ?? throw new InvalidOperationException("Authentication audience was not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = configuration.GetValue(
                    "Authentication:RequireHttpsMetadata",
                    !environment.IsDevelopment());
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddTransient<IClaimsTransformation, KeycloakRealmRoleClaimsTransformation>();
        services.AddAuthorizationBuilder()
            .AddPolicy(JobFlowPolicies.UserAccess, policy =>
                policy.RequireRole(JobFlowRoles.User, JobFlowRoles.Admin));

        return services;
    }
}

public sealed class KeycloakRealmRoleClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        var realmAccess = identity?.FindFirst("realm_access")?.Value;

        if (identity is null || string.IsNullOrWhiteSpace(realmAccess))
        {
            return Task.FromResult(principal);
        }

        using var document = JsonDocument.Parse(realmAccess);

        if (!document.RootElement.TryGetProperty("roles", out var roles)
            || roles.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(principal);
        }

        foreach (var role in roles.EnumerateArray())
        {
            var roleName = role.GetString();

            if (!string.IsNullOrWhiteSpace(roleName)
                && !identity.HasClaim(ClaimTypes.Role, roleName))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
            }
        }

        return Task.FromResult(principal);
    }
}
