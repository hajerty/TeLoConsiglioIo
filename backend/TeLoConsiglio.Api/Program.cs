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
const string LegacyDefaultJwtKey = "DevOnly_ChangeMe_TeLoConsiglio_SuperSecret_Key_12345!";
var rawJwtKey = builder.Configuration["JWT_KEY"] ?? builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(rawJwtKey))
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(
            "JWT_KEY non configurata. In Production e' obbligatorio impostare la variabile d'ambiente JWT_KEY con almeno 32 byte casuali.");
    rawJwtKey = LegacyDefaultJwtKey;
    Console.WriteLine("[WARN] JWT_KEY non impostata: uso chiave di sviluppo. NON usare in produzione.");
}

if (builder.Environment.IsProduction())
{
    if (System.Text.Encoding.UTF8.GetByteCount(rawJwtKey) < 32)
        throw new InvalidOperationException("JWT_KEY troppo corta: in Production servono almeno 32 byte UTF-8.");
    if (string.Equals(rawJwtKey, LegacyDefaultJwtKey, StringComparison.Ordinal))
        throw new InvalidOperationException("JWT_KEY usa il valore di default committato in repo. Impossibile avviare in Production.");
}

var jwtSettings = new JwtSettings
{
    Key = rawJwtKey,
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

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("RequireCapogruppoOrAdmin", p =>
        p.RequireRole(Roles.Admin, Roles.Capogruppo, Roles.Vice));
    opt.AddPolicy("RequireAdminOnly", p =>
        p.RequireRole(Roles.Admin));
});

// ----- App services -----
builder.Services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
builder.Services.AddHttpClient<IAIService, GeminiAIService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ----- API -----
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Pulisci la risposta di validazione 400: rimuovi "dto: The dto field is required."
// quando ci sono gia' errori puntuali sui campi del body.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(opt =>
{
    opt.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value!.Errors.Select(e =>
                    string.IsNullOrEmpty(e.ErrorMessage) ? (e.Exception?.Message ?? "Valore non valido") : e.ErrorMessage
                ).ToArray());

        // Se esiste sia "dto" generico sia errori specifici di campo, scarta il generico.
        var hasFieldErrors = errors.Keys.Any(k => !string.Equals(k, "dto", StringComparison.OrdinalIgnoreCase));
        if (hasFieldErrors)
            errors.Remove("dto");

        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
            errors.ToDictionary(kv => kv.Key, kv => kv.Value as string[]))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validazione fallita"
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
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
    opt.AddDefaultPolicy(p =>
    {
        var frontendUrl = builder.Configuration["FRONTEND_URL"]
            ?? builder.Configuration["Frontend:Url"];

        if (builder.Environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(frontendUrl))
                throw new InvalidOperationException(
                    "FRONTEND_URL non configurato in Production. CORS richiede l'origine esplicita del frontend.");
            p.WithOrigins(frontendUrl);
        }
        else
        {
            p.WithOrigins(
                frontendUrl ?? "http://localhost:5173",
                "http://localhost:5173",
                "http://localhost:3000");
        }

        p.AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    });
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
