using AuthService.Authorization;
using AuthService.Configuration;
using AuthService.Data;
using AuthService.Middleware;
using AuthService.Security;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using StackExchange.Redis;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
AddDotEnvToConfiguration(builder);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1"
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter: Bearer {your JWT access token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [bearerScheme] = Array.Empty<string>()
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AuthCookieOptions>(builder.Configuration.GetSection("AuthCookie"));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.Configure<TokenTtlOptions>(builder.Configuration.GetSection("TokenTtl"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));

var authDbConnectionString = ResolveAuthDbConnectionString(builder.Configuration);

builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var version = builder.Configuration["MySql:ServerVersion"] ?? "8.0.36";
    options.UseMySql(authDbConnectionString, new MySqlServerVersion(Version.Parse(version)));
});

builder.Services.AddHttpContextAccessor();

var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConn);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<ISecretProvider, StaticSecretProvider>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenGenerator, TokenGenerator>();
builder.Services.AddScoped<ITokenHasher, TokenHasher>();

builder.Services.AddScoped<ISigningKeyProvider, InMemorySigningKeyProvider>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IJwtValidator, JwtValidator>();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService.Services.AuthService>();
builder.Services.AddScoped<IAuditService, AuditService>();
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddScoped<IRateLimiter, RedisRateLimiter>();
}
else
{
    builder.Services.AddScoped<IRateLimiter, MemoryRateLimiter>();
}
builder.Services.AddScoped<ISessionValidator, SessionValidator>();
builder.Services.AddScoped<ISessionCache, SessionCache>();
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (!string.IsNullOrWhiteSpace(emailOptions.SmtpHost) && !string.IsNullOrWhiteSpace(emailOptions.FromEmail))
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, NoopEmailSender>();
}

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var keyProvider = new InMemorySigningKeyProvider(Microsoft.Extensions.Options.Options.Create(jwtOptions));

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKeyResolver = (_, token, kid, _) =>
            {
                var keys = keyProvider.GetAll()
                    .Select(k => new SymmetricSecurityKey(k.Secret) { KeyId = k.KeyId })
                    .ToList();
                return keys;
            }
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtBearer");
                try
                {
                    var user = context.Principal!;
                    await validator.ValidateAsync(user.GetSessionId(), user.GetTokenVersion(), user.GetTenantId(), user.GetUserId(), context.HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    var principal = context.Principal;
                    var sub = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var sid = principal?.FindFirst(AuthService.Security.AuthClaims.SessionId)?.Value;
                    var tid = principal?.FindFirst(AuthService.Security.AuthClaims.TenantId)?.Value
                              ?? principal?.FindFirst(AuthService.Security.AuthClaims.TenantIdMs)?.Value;
                    var tokenVersion = principal?.FindFirst(AuthService.Security.AuthClaims.TokenVersion)?.Value;
                    logger.LogWarning(ex,
                        "JWT token validation failed. sub={Sub} sid={Sid} tid={Tid} tokenVersion={TokenVersion}",
                        sub, sid, tid, tokenVersion);
                    context.Fail(ex.Message);
                }
            }
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

var app = builder.Build();

await EnsureMySqlDatabaseExistsAsync(authDbConnectionString);

await ApplyMigrationsWithRecoveryAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
    options.RoutePrefix = "swagger";
});

app.UseDefaultFiles();
app.UseStaticFiles();

if (corsOrigins.Length > 0)
{
    app.UseCors();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddDotEnvToConfiguration(WebApplicationBuilder builder)
{
    var candidates = new List<string>
    {
        Path.Combine(builder.Environment.ContentRootPath, ".env"),
        Path.Combine(Directory.GetCurrentDirectory(), ".env")
    };

    var parent = Directory.GetParent(builder.Environment.ContentRootPath)?.FullName;
    if (!string.IsNullOrWhiteSpace(parent))
    {
        candidates.Add(Path.Combine(parent, ".env"));
    }

    foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (!File.Exists(path))
        {
            continue;
        }

        var values = ParseDotEnv(path);
        if (values.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(values);
        }
    }
}

static Dictionary<string, string?> ParseDotEnv(string path)
{
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var lines = File.ReadAllLines(path);

    foreach (var raw in lines)
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
        {
            continue;
        }

        if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            line = line["export ".Length..].Trim();
        }

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            continue;
        }

        var key = line[..equalsIndex].Trim();
        if (key.Length == 0)
        {
            continue;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            continue;
        }

        var value = line[(equalsIndex + 1)..].Trim();
        value = NormalizeDotEnvValue(value);
        values[key] = value;
        if (key.Contains("__", StringComparison.Ordinal))
        {
            var normalizedKey = key.Replace("__", ":", StringComparison.Ordinal);
            values[normalizedKey] = value;
        }
    }

    return values;
}

static string NormalizeDotEnvValue(string value)
{
    if (value.Length == 0)
    {
        return value;
    }

    if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
        (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal)))
    {
        return value[1..^1];
    }

    var commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
    if (commentIndex >= 0)
    {
        return value[..commentIndex].TrimEnd();
    }

    return value;
}

static string ResolveAuthDbConnectionString(IConfiguration configuration)
{
    var conn = configuration.GetConnectionString("AuthDb") ?? string.Empty;
    var envConn = configuration["AUTH_DB_CONNECTION"];
    if (!string.IsNullOrWhiteSpace(envConn))
    {
        conn = envConn;
    }

    if (conn.Contains("your_password", StringComparison.OrdinalIgnoreCase))
    {
        var injectedPassword = configuration["MYSQL_ROOT_PASSWORD"] ?? configuration["DB_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(injectedPassword))
        {
            conn = conn.Replace("your_password", injectedPassword, StringComparison.OrdinalIgnoreCase);
        }
    }

    if (string.IsNullOrWhiteSpace(conn) || conn.Contains("your_password", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "AuthDb connection string is not configured. Set ConnectionStrings:AuthDb or AUTH_DB_CONNECTION with a real MySQL password.");
    }

    return conn;
}

static async Task EnsureMySqlDatabaseExistsAsync(string connectionString)
{
    var csb = new MySqlConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(csb.Database))
    {
        return;
    }

    var databaseName = csb.Database;
    var adminCsb = new MySqlConnectionStringBuilder(connectionString)
    {
        Database = string.Empty
    };

    await using var conn = new MySqlConnection(adminCsb.ConnectionString);
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName.Replace("`", "``", StringComparison.Ordinal)}`;";
    await cmd.ExecuteNonQueryAsync();
}

static async Task ApplyMigrationsWithRecoveryAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("StartupMigration");

    try
    {
        await db.Database.MigrateAsync();
        return;
    }
    catch (MySqlException ex) when (LooksLikeExistingSchemaConflict(ex))
    {
        logger.LogWarning(ex,
            "Schema conflict detected while applying migrations. Attempting EF baseline recovery.");
    }

    var recovered = await TryBaselineExistingSchemaAsync(db);
    if (!recovered)
    {
        throw new InvalidOperationException(
            "Database schema appears to exist but migration history could not be auto-recovered. " +
            "Ensure __EFMigrationsHistory is in sync with the current schema.");
    }

    await db.Database.MigrateAsync();
}

static bool LooksLikeExistingSchemaConflict(MySqlException ex)
{
    return ex.Number == 1050 ||
           ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
}

static async Task<bool> TryBaselineExistingSchemaAsync(AuthDbContext db)
{
    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    if (applied.Count > 0)
    {
        return false;
    }

    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    var initialMigration = pending.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(initialMigration) ||
        !pending.Contains(initialMigration, StringComparer.Ordinal))
    {
        return false;
    }

    if (!await HasExistingAppTablesAsync(db))
    {
        return false;
    }

    var productVersion = typeof(DbContext).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion?
        .Split('+', 2)[0] ?? "9.0.0";

    await db.Database.ExecuteSqlInterpolatedAsync(
        $"INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ({initialMigration}, {productVersion});");

    return true;
}

static async Task<bool> HasExistingAppTablesAsync(AuthDbContext db)
{
    var connString = db.Database.GetConnectionString();
    if (string.IsNullOrWhiteSpace(connString))
    {
        return false;
    }

    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = DATABASE()
          AND table_name <> '__EFMigrationsHistory'
        LIMIT 1;
        """;

    var result = await cmd.ExecuteScalarAsync();
    return result is not null && result != DBNull.Value;
}
