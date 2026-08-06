using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is required.");
var audience = builder.Configuration["Keycloak:Audience"]
    ?? throw new InvalidOperationException("Keycloak:Audience is required.");
var requireHttpsMetadataValue = builder.Configuration["Keycloak:RequireHttpsMetadata"];

if (!bool.TryParse(requireHttpsMetadataValue, out var requireHttpsMetadata))
{
    throw new InvalidOperationException("Keycloak:RequireHttpsMetadata must be true or false.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = requireHttpsMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = authority.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                var realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                if (string.IsNullOrWhiteSpace(realmAccess))
                {
                    return Task.CompletedTask;
                }

                using var document = JsonDocument.Parse(realmAccess);
                if (!document.RootElement.TryGetProperty("roles", out var roles))
                {
                    return Task.CompletedTask;
                }

                foreach (var role in roles.EnumerateArray())
                {
                    var roleName = role.GetString();
                    if (!string.IsNullOrWhiteSpace(roleName))
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Keycloak Demo API",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter a Keycloak access token.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/public", () => Results.Ok(new
{
    message = "This endpoint is public."
}));

app.MapGet("/api/profile", (ClaimsPrincipal user) => Results.Ok(new
{
    message = "You are authenticated.",
    username = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct()
}))
.RequireAuthorization();

app.MapGet("/api/admin", (ClaimsPrincipal user) => Results.Ok(new
{
    message = "You have the admin role.",
    username = user.Identity?.Name
}))
.RequireAuthorization(policy => policy.RequireRole("admin"));

app.Run();
