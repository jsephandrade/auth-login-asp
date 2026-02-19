using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true);

        AddDotEnv(configBuilder, basePath);
        configBuilder.AddEnvironmentVariables();
        var config = configBuilder.Build();

        var conn = ResolveAuthDbConnectionString(config);
        var versionString = config["MySql:ServerVersion"] ?? "8.0.36";
        var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
        optionsBuilder.UseMySql(conn, new MySqlServerVersion(Version.Parse(versionString)));

        return new AuthDbContext(optionsBuilder.Options);
    }

    private static string ResolveAuthDbConnectionString(IConfiguration configuration)
    {
        var conn = configuration["AUTH_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(conn))
        {
            conn = configuration.GetConnectionString("AuthDb");
        }

        if (!string.IsNullOrWhiteSpace(conn) &&
            conn.Contains("your_password", StringComparison.OrdinalIgnoreCase))
        {
            var injectedPassword = configuration["MYSQL_ROOT_PASSWORD"] ?? configuration["DB_PASSWORD"];
            if (!string.IsNullOrWhiteSpace(injectedPassword))
            {
                conn = conn.Replace("your_password", injectedPassword, StringComparison.OrdinalIgnoreCase);
            }
        }

        if (string.IsNullOrWhiteSpace(conn) ||
            conn.Contains("your_password", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AuthDb connection string is not configured. Set AUTH_DB_CONNECTION or ConnectionStrings:AuthDb.");
        }

        return conn;
    }

    private static void AddDotEnv(IConfigurationBuilder builder, string basePath)
    {
        var candidates = new List<string>
        {
            Path.Combine(basePath, ".env")
        };

        var parent = Directory.GetParent(basePath)?.FullName;
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
                builder.AddInMemoryCollection(values);
            }
        }
    }

    private static Dictionary<string, string?> ParseDotEnv(string path)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
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

            var value = NormalizeDotEnvValue(line[(equalsIndex + 1)..].Trim());
            values[key] = value;

            if (key.Contains("__", StringComparison.Ordinal))
            {
                values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
            }
        }

        return values;
    }

    private static string NormalizeDotEnvValue(string value)
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
}
