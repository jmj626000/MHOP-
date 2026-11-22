using Dapper;
using Microsoft.AspNetCore.Http;
using Mhop.Data;
using Mhop.Security;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

/// <summary>公告：用户端公开读取已启用公告；管理端 CRUD 需 announcements 模块权限。</summary>
public static class AnnouncementEndpoints
{
    public static void MapAnnouncements(this IEndpointRouteBuilder app)
    {
        // 公开：用户端获取已启用公告（按 sort 降序、创建时间降序），无需登录
        app.MapGet("/api/announcements", (Database db) =>
        {
            using var conn = db.Open();
            var list = conn.Query<Announcement>(
                    "SELECT * FROM announcements WHERE is_active = 1 ORDER BY sort DESC, created_at DESC LIMIT 50")
                .ToList();
            return Results.Json(list.Select(AnnouncementOut.From).ToList());
        });

        // 管理端：列表（含未启用）
        app.MapGet("/api/admin/announcements", (HttpContext ctx, Database db, CurrentUserProvider users) =>
        {
            var err = CheckPerm(ctx, users);
            if (err is not null) return err;
            using var conn = db.Open();
            var list = conn.Query<Announcement>(
                    "SELECT * FROM announcements ORDER BY sort DESC, created_at DESC LIMIT 200")
                .ToList();
            return Results.Json(list.Select(AnnouncementOut.From).ToList());
        });

        // 管理端：创建
        app.MapPost("/api/admin/announcements", (AnnouncementIn body, HttpContext ctx, Database db, CurrentUserProvider users) =>
        {
            var err = CheckPerm(ctx, users);
            if (err is not null) return err;
            var title = body.Title.Trim();
            var content = body.Content.Trim();
            if (title.Length == 0) return HttpResults.Error(400, "标题不能为空");
            if (content.Length == 0) return HttpResults.Error(400, "内容不能为空");
            if (title.Length > 200) return HttpResults.Error(400, "标题不能超过 200 字");
            var now = DateTime.Now;
            using var conn = db.Open();
            var id = (int)conn.ExecuteScalar<long>(
                "INSERT INTO announcements (title, content, is_active, sort, created_at, updated_at) " +
                "VALUES (@Title, @Content, @IsActive, @Sort, @Now, @Now); SELECT last_insert_rowid();",
                new { Title = title, Content = content, body.IsActive, body.Sort, Now = now });
            return Results.Json(new { id });
        });

        // 管理端：更新
        app.MapPut("/api/admin/announcements/{id:int}", (int id, AnnouncementIn body, HttpContext ctx, Database db, CurrentUserProvider users) =>
        {
            var err = CheckPerm(ctx, users);
            if (err is not null) return err;
            if (Load(db, id) is null) return HttpResults.Error(404, "公告不存在");
            var title = body.Title.Trim();
            var content = body.Content.Trim();
            if (title.Length == 0) return HttpResults.Error(400, "标题不能为空");
            if (content.Length == 0) return HttpResults.Error(400, "内容不能为空");
            if (title.Length > 200) return HttpResults.Error(400, "标题不能超过 200 字");
            using var conn = db.Open();
            conn.Execute(
                "UPDATE announcements SET title = @Title, content = @Content, is_active = @IsActive, sort = @Sort, updated_at = @Now WHERE id = @Id",
                new { Id = id, Title = title, Content = content, body.IsActive, body.Sort, Now = DateTime.Now });
            return Results.Json(new { ok = true });
        });

        // 管理端：删除
        app.MapDelete("/api/admin/announcements/{id:int}", (int id, HttpContext ctx, Database db, CurrentUserProvider users) =>
        {
            var err = CheckPerm(ctx, users);
            if (err is not null) return err;
            if (Load(db, id) is null) return HttpResults.Error(404, "公告不存在");
            using var conn = db.Open();
            conn.Execute("DELETE FROM announcements WHERE id = @Id", new { Id = id });
            return Results.Json(new { ok = true });
        });
    }

    /// <summary>校验当前用户是否拥有公告管理权限；返回 null 表示通过，否则返回错误响应。</summary>
    private static IResult? CheckPerm(HttpContext ctx, CurrentUserProvider users)
    {
        var u = users.Current(ctx);
        if (u is null) return HttpResults.Error(401, "请先登录");
        if (!AdminPermissions.Has(u, AdminPermissions.Announcements))
            return HttpResults.Error(403, $"没有「{AdminPermissions.Labels[AdminPermissions.Announcements]}」模块权限");
        return null;
    }

    private static Announcement? Load(Database db, int id)
    {
        using var conn = db.Open();
        return conn.QueryFirstOrDefault<Announcement>("SELECT * FROM announcements WHERE id = @Id", new { Id = id });
    }
}
