using System.Text.Json.Serialization;

namespace Mhop.Configuration;

/// <summary>
/// 与 Python 版同名的环境变量配置；启动时读取 .env（若存在），再由环境变量覆盖。
/// </summary>
public sealed class AppSettings
{
    public string AppName { get; }
    public string DatabaseUrl { get; }
    public string JwtSecret { get; }
    public string JwtAlg { get; }
    public int JwtExpireHours { get; }
    public string LlmBaseUrl { get; }
    public string LlmApiKey { get; }
    public string LlmModel { get; }
    public string LekeHotline { get; }
    public string SmtpHost { get; }
    public int SmtpPort { get; }
    public string SmtpUser { get; }
    public string SmtpPassword { get; }
    public string SmtpFrom { get; }
    public bool SmtpUseSsl { get; }
    public string StaticDir { get; }
    public string UploadDir { get; }

    public bool IsSqlite => DatabaseUrl.StartsWith("sqlite", StringComparison.OrdinalIgnoreCase);

    /// <summary>把 SQLAlchemy 风格 sqlite:/// 路径解析为实际文件路径。</summary>
    public string SqlitePath
    {
        get
        {
            // sqlite:///./mhop.db  -> ./mhop.db
            // sqlite:////opt/x.db  -> /opt/x.db
            var rest = DatabaseUrl["sqlite:///".Length..];
            return rest;
        }
    }

    public string StaticDirAbs => Path.GetFullPath(StaticDir);
    public string UploadDirAbs => Path.GetFullPath(UploadDir);

    public AppSettings()
    {
        DotEnv.TryLoad(Path.Combine(AppContext.BaseDirectory, ".env"));
        DotEnv.TryLoad(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        AppName = Get("APP_NAME", "MHOP 公益心理辅助平台");
        DatabaseUrl = Get("DATABASE_URL", "sqlite:///./mhop.db");
        JwtSecret = Get("JWT_SECRET", "dev-only-change-me-in-production");
        JwtAlg = Get("JWT_ALG", "HS256");
        JwtExpireHours = GetInt("JWT_EXPIRE_HOURS", 72);
        LlmBaseUrl = Get("LLM_BASE_URL", "");
        LlmApiKey = Get("LLM_API_KEY", "");
        LlmModel = Get("LLM_MODEL", "glm-4-flash");
        LekeHotline = Get("LEKE_HOTLINE", "");
        SmtpHost = Get("SMTP_HOST", "");
        SmtpPort = GetInt("SMTP_PORT", 465);
        SmtpUser = Get("SMTP_USER", "");
        SmtpPassword = Get("SMTP_PASSWORD", "");
        SmtpFrom = Get("SMTP_FROM", "");
        SmtpUseSsl = GetBool("SMTP_USE_SSL", true);
        StaticDir = Get("STATIC_DIR", "static");
        UploadDir = Get("UPLOAD_DIR", "uploads");
    }

    private static string Get(string key, string def)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    private static int GetInt(string key, int def) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;

    private static bool GetBool(string key, bool def)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(v)) return def;
        return v is "1" or "true" or "True" or "TRUE" or "yes";
    }
}

/// <summary>极简 .env 解析器（KEY=VALUE，# 注释），不引入第三方依赖。
/// 语义对齐 python-dotenv：同一键重复时后者覆盖；真实环境变量优先于文件。</summary>
public static class DotEnv
{
    private static readonly HashSet<string> RealEnvKeys = new(Environment.GetEnvironmentVariables().Keys.Cast<string>());

    public static void TryLoad(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('=')) continue;
            var idx = line.IndexOf('=');
            var key = line[..idx].Trim();
            var val = line[(idx + 1)..].Trim().Trim('"').Trim('\'');
            if (key.Length > 0 && !RealEnvKeys.Contains(key))
                Environment.SetEnvironmentVariable(key, val);
        }
    }
}

/// <summary>热线条目（对应 Python config.HOTLINES）。</summary>
public sealed record Hotline(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("tag")] string Tag,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("level")] string Level);
