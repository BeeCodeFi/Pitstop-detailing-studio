using System.Text;
using DataEntry.Api.Data;
using DataEntry.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
// Use DATABASE_URL (PostgreSQL) in production, SQLite locally
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        string npgsqlConnectionString;
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            // Render provides DATABASE_URL as postgres://user:pass@host:port/db
            // Use Split(':', 2) so passwords containing ':' are preserved
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':', 2);
            var port = uri.Port > 0 ? uri.Port : 5432;
            npgsqlConnectionString = $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : "")};SSL Mode=Require;Trust Server Certificate=true";
        }
        else
        {
            // Already in Npgsql key=value format (some Render plans output this)
            npgsqlConnectionString = databaseUrl;
        }
        options.UseNpgsql(npgsqlConnectionString);
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DaybookService>();
builder.Services.AddScoped<ReportService>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — allow React dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var envOrigins = (Environment.GetEnvironmentVariable("CORS_ORIGINS")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            ?? Array.Empty<string>();

        var configuredOrigins = builder.Configuration
            .GetSection("Cors:Origins").Get<string[]>()
            ?? Array.Empty<string>();

        var explicitOrigins = envOrigins.Length > 0 ? envOrigins : configuredOrigins;

        policy.SetIsOriginAllowed(origin =>
            {
                // always allow localhost dev
                if (origin.StartsWith("http://localhost") || origin.StartsWith("https://localhost"))
                    return true;
                // allow any Vercel preview or production domain
                if (origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                    return true;
                // allow explicitly configured origins
                return explicitOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    var dbProvider = db.Database.ProviderName ?? "unknown";
    var dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    logger.LogInformation("DB provider: {Provider} | DATABASE_URL set: {IsSet}", dbProvider, !string.IsNullOrEmpty(dbUrl));
    try
    {
        db.Database.Migrate();
        logger.LogInformation("Database migration completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed: {Message}", ex.Message);
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new {
    status = "ok",
    database = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")) ? "sqlite" : "postgresql",
    timestamp = DateTime.UtcNow
}));
app.MapControllers();

app.Run();
