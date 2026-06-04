using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TeLoConsiglio.Api.Auth;
using TeLoConsiglio.Api.Seed;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ----- Configuration: prefer env vars -----
builder.Configuration.AddEnvironmentVariables();

// ----- DB -----
var connString = builder.Configuration["ConnectionStrings:Default"]
                 ?? builder.Configuration["DATABASE_URL"]
                 ?? "Host=localhost;Port=5432;Database=teloconsiglio;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connString, npg => npg.MigrationsAssembly("TeLoConsiglio.Infrastructure")));

// ----- Identity -----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
    {
        opt.Password.RequireDigit = true;
        opt.Password.RequiredLength = 8;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireLowercase = true;
        opt.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ----- JWT -----
var jwtSettings = new JwtSettings
{
    Key = builder.Configuration["JWT_KEY"] ?? builder.Configuration["Jwt:Key"] ?? "DevOnly_ChangeMe_TeLoConsiglio_SuperSecret_Key_12345!",
    Issuer = builder.Configuration["JWT_ISSUER"] ?? "TeLoConsiglio",
    Audience = builder.Configuration["JWT_AUDIENCE"] ?? "TeLoConsiglio",
    ExpirationMinutes = int.TryParse(builder.Configuration["JWT_EXP_MIN"], out var e) ? e : 60,
    RefreshExpirationDays = int.TryParse(builder.Configuration["JWT_REFRESH_DAYS"], out var d) ? d : 14
};
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<JwtTokenService>();

var authBuilder = builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});

// Optional OAuth (only registered if credentials present)
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"];
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(opt =>
    {
        opt.ClientId = googleClientId;
        opt.ClientSecret = googleClientSecret;
    });
}

var msClientId = builder.Configuration["MICROSOFT_CLIENT_ID"];
var msClientSecret = builder.Configuration["MICROSOFT_CLIENT_SECRET"];
if (!string.IsNullOrWhiteSpace(msClientId) && !string.IsNullOrWhiteSpace(msClientSecret))
{
    authBuilder.AddMicrosoftAccount(opt =>
    {
        opt.ClientId = msClientId;
        opt.ClientSecret = msClientSecret;
    });
}

builder.Services.AddAuthorization();

// ----- App services -----
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddHttpClient<IAnthropicService, AnthropicService>();

// ----- API -----
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TeLoConsiglio API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Inserisci il JWT (senza 'Bearer ' prefix)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .WithOrigins(
            builder.Configuration["FRONTEND_URL"] ?? "http://localhost:5173",
            "http://localhost:5173",
            "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// ----- DB migrate + seed -----
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = sp.GetRequiredService<AppDbContext>();
        for (int i = 0; i < 30; i++)
        {
            try
            {
                db.Database.Migrate();
                break;
            }
            catch (Exception ex) when (i < 29)
            {
                logger.LogWarning("DB non pronto ({Attempt}): {Msg}", i + 1, ex.Message);
                Thread.Sleep(2000);
            }
        }
        await DataSeeder.SeedAsync(sp, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore in migrate/seed");
    }
}

if (app.Environment.IsDevelopment() || builder.Configuration["ENABLE_SWAGGER"] == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsRoot);

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new { name = "TeLoConsiglio.Api", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }
