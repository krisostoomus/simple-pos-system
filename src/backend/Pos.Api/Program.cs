using System.Text;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pos.Api.Endpoints;
using Pos.Api.Errors;
using Pos.Api.OpenApi;
using Pos.Infrastructure;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.RealTime;
using Pos.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// AuthN/AuthZ
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        };
    });
builder.Services.AddAuthorization(o =>
    o.AddPolicy("staff", p => p.RequireRole(TokenService.StaffRole)));

// API versioning
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;
});

// OpenAPI + problem details + cors + signalr + health
builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddDbContextCheck<PosDbContext>();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:8080"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI document + Swagger UI
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "POS API v1"));

// Versioned API group: /api/v1
var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build();
var v1 = app.MapGroup("/api/v{version:apiVersion}")
    .WithApiVersionSet(versionSet)
    .HasApiVersion(new ApiVersion(1, 0));

v1.MapAuthEndpoints();
v1.MapProductEndpoints();
v1.MapOrderEndpoints();
v1.MapReportEndpoints();

app.MapHub<StockHub>("/hubs/stock");
app.MapHealthChecks("/health");

app.MapGet("/", () => Results.Ok(new
{
    name = "Charity Bake Sale POS API",
    version = "v1",
    links = new { swagger = "/swagger", openapi = "/openapi/v1.json", health = "/health", token = "/api/v1/auth/token" }
})).ExcludeFromDescription();

// Migrate + seed on startup (single instance).
if (!app.Environment.IsEnvironment("IntegrationTest") ||
    app.Configuration.GetValue("RunMigrationsOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    // seed.json is copied to the build output, so resolve relative paths against the app base dir.
    await seeder.SeedAsync(AppContext.BaseDirectory);
}

app.Run();

public partial class Program;
