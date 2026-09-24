using System.Text.Json.Serialization;
using System.Text.Unicode;
using Mhop.Configuration;
using Mhop.Data;
using Mhop.Endpoints;
using Mhop.Security;
using Mhop.Services;
using Mhop.Web;

// SQL 列名全为 snake_case（created_at、is_ai…），实体属性为 PascalCase
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// ---- JSON：与 FastAPI 默认行为对齐（snake_case 由 DTO 特性保证；中文不转义；naive 时间不带 Z） ----
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    options.SerializerOptions.Converters.Add(new NaiveDateTimeConverter());
    options.SerializerOptions.Converters.Add(new NaiveNullableDateTimeConverter());
});

// 与 Python 版一致的开放 CORS（同源部署为主，不含凭据）
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ---- DI（与 Python 模块一一对应的单例服务） ----
builder.Services.AddSingleton<AppSettings>();
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<JwtTokens>();
builder.Services.AddSingleton<CurrentUserProvider>();
builder.Services.AddSingleton<OnlineTracker>();
builder.Services.AddSingleton<EmailCodeService>();
builder.Services.AddHttpClient<AiService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddSingleton<ForumService>();

var app = builder.Build();

// ---- 启动：建表 + 迁移 + 种子（对应 FastAPI lifespan） ----
var db = app.Services.GetRequiredService<Database>();
db.CreateTables();
db.MigrateSchema();
app.Services.GetRequiredService<SeedService>().EnsureSeed();

app.UseCors();

// ---- 37 个 API ----
app.MapCommon();
app.MapAuth();
app.MapForum();
app.MapAssessment();
app.MapAdmin();
app.MapUpload();

// ---- SPA 静态托管兜底（必须最后注册） ----
app.MapSpaFallback();

var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrEmpty(urls))
    app.Urls.Add("http://0.0.0.0:10000");

app.Run();
