using Microsoft.AspNetCore.StaticFiles;
using Mhop.Configuration;

namespace Mhop.Web;

/// <summary>
/// SPA 兜底（对应 main.py 末尾的 spa_entry）：
/// /api/* 未命中 → 404 JSON；/uploads/* → 上传目录文件；
/// assets/* 带永久缓存；其余一律回退 index.html（no-cache）。
/// </summary>
public static class SpaEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static void MapSpaFallback(this IEndpointRouteBuilder app)
    {
        // 不能用 MapFallback：其默认模板带 nonfile 约束，/uploads/x.webp、/assets/x.js
        // 这类“像文件”的请求不会进兜底。全捕获 GET 路由优先级低于上面的精确 API 路由。
        app.MapGet("{**catchAll}", (HttpContext ctx, AppSettings settings) =>
        {
            var raw = ctx.Request.Path.Value?.TrimStart('/') ?? "";

            if (raw.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
                return HttpResults.Error(404, "Not Found");

            // 用户上传文件
            if (raw.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var rel = raw["uploads/".Length..];
                var uploadFile = SafeResolve(settings.UploadDirAbs, rel);
                if (uploadFile is not null)
                    return ServeFile(uploadFile, null);
                return HttpResults.Error(404, "Not Found");
            }

            // 前端构建产物
            var staticDir = settings.StaticDirAbs;
            if (Directory.Exists(staticDir))
            {
                var candidate = SafeResolve(staticDir, raw);
                if (candidate is not null)
                {
                    if (raw.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                        return ServeFile(candidate, "public, max-age=31536000, immutable");
                    return ServeFile(candidate, null);
                }

                var index = Path.Combine(staticDir, "index.html");
                if (File.Exists(index))
                    return ServeFile(index, "no-cache, must-revalidate");
            }

            return HttpResults.Error(404, "Not Found");
        });
    }

    /// <summary>拼合目录内文件并阻止路径穿越；不是常规文件或越界时返回 null。</summary>
    private static string? SafeResolve(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(full) ? full : null;
    }

    private static IResult ServeFile(string path, string? cacheControl)
    {
        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";
        return Results.File(path, contentType, enableRangeProcessing: true)
            .WithCacheHeader(cacheControl);
    }

    private static IResult WithCacheHeader(this IResult result, string? cacheControl)
    {
        if (cacheControl is not null)
            return new CachedFileResult(result, cacheControl);
        return result;
    }

    private sealed class CachedFileResult : IResult
    {
        private readonly IResult _inner;
        private readonly string _cacheControl;

        public CachedFileResult(IResult inner, string cacheControl)
        {
            _inner = inner;
            _cacheControl = cacheControl;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = _cacheControl;
            await _inner.ExecuteAsync(httpContext);
        }
    }
}
