using Dapper;
using Microsoft.AspNetCore.Http;
using Mhop.Data;
using Mhop.Security;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin");
        g.MapGet("/stats", Stats);
        g.MapGet("/posts", ListPosts);
        g.MapPost("/posts/{postId:int}/moderate", ModeratePost);
        g.MapGet("/replies", ListReplies);
        g.MapPost("/replies/{replyId:int}/moderate", ModerateReply);
        g.MapPost("/replies/{replyId:int}/recall", RecallReply);
        g.MapPost("/replies/{replyId:int}/restore", RestoreReply);
        g.MapGet("/users", ListUsers);
        g.MapPost("/users/{userId:int}/status", SetUserStatus);
        g.MapPost("/users/{userId:int}/badge", SetUserBadge);
        g.MapPost("/users/{userId:int}/role", SetUserRole);
        g.MapPost("/users/{userId:int}/reset-password", ResetUserPassword);
        g.MapGet("/ai-logs", ListAiLogs);
    }

    /// <summary>对应 get_admin：未登录 401，非管理员 403。</summary>
    private static User? RequireAdmin(HttpContext ctx, CurrentUserProvider users, out IResult? error)
    {
        var u = users.Optional(ctx);
        if (u is null)
        {
            error = HttpResults.Error(401, "请先登录");
            return null;
        }
        if (u.Role != "admin")
        {
            error = HttpResults.Error(403, "需要管理员权限");
            return null;
        }
        error = null;
        return u;
    }

    private static IResult Stats(HttpContext ctx, Database db, OnlineTracker online, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var today = DateTime.Now.AddHours(-24);
        long C(string sql, object? p = null) => conn.ExecuteScalar<long>(sql, p ?? new { });

        return Results.Json(new
        {
            users = C("SELECT COUNT(*) FROM users"),
            posts = C("SELECT COUNT(*) FROM posts"),
            replies = C("SELECT COUNT(*) FROM replies"),
            assessments = C("SELECT COUNT(*) FROM assessments"),
            ai_logs = C("SELECT COUNT(*) FROM ai_logs"),
            pending_posts = C("SELECT COUNT(*) FROM posts WHERE status = 0"),
            crisis_posts = C("SELECT COUNT(*) FROM posts WHERE crisis = 1 AND status != 2"),
            pending_replies = C("SELECT COUNT(*) FROM replies WHERE status = 0 AND is_ai = 0"),
            rejected_replies = C("SELECT COUNT(*) FROM replies WHERE status = 2"),
            new_posts_24h = C("SELECT COUNT(*) FROM posts WHERE created_at >= @T", new { T = today }),
            new_users_24h = C("SELECT COUNT(*) FROM users WHERE created_at >= @T", new { T = today }),
            online = online.Count(),
        });
    }

    /// <summary>后台视角：匿名也返回真实作者与手机号（历史帖 user_id 已丢失 → null/null）。</summary>
    private static (string? Name, string? Phone) RealAuthor(System.Data.IDbConnection conn, int? userId)
    {
        if (userId is null) return (null, null);
        var u = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (u is null) return ("未知用户", null);
        return (u.Username, string.IsNullOrEmpty(u.Phone) ? null : u.Phone);
    }

    private sealed class ReplyCountRow
    {
        public int PostId { get; set; }
        public int Cnt { get; set; }
    }

    private static IResult ListPosts(
        int? status, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var posts = status is null
            ? conn.Query<Post>("SELECT * FROM posts ORDER BY created_at DESC LIMIT 200").ToList()
            : conn.Query<Post>("SELECT * FROM posts WHERE status = @Status ORDER BY created_at DESC LIMIT 200",
                new { Status = status.Value }).ToList();

        var postIds = posts.Select(p => p.Id).ToList();
        var replyCounts = postIds.Count == 0
            ? new Dictionary<int, int>()
            : conn.Query<ReplyCountRow>(
                    "SELECT post_id AS PostId, COUNT(*) AS Cnt FROM replies WHERE post_id IN @Ids GROUP BY post_id",
                    new { Ids = postIds })
                .ToDictionary(r => r.PostId, r => r.Cnt);

        var rows = posts.Select(p =>
        {
            var (author, phone) = RealAuthor(conn, p.UserId);
            return new
            {
                id = p.Id,
                content = p.Content,
                board = p.Board,
                status = p.Status,
                crisis = p.Crisis,
                is_anonymous = p.IsAnonymous,
                author,
                author_phone = phone,
                review_note = p.ReviewNote ?? "",
                reply_count = replyCounts.GetValueOrDefault(p.Id),
                created_at = p.CreatedAt,
            };
        }).ToList();
        return Results.Json(rows);
    }

    private static IResult ModeratePost(
        int postId, ModerateIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var post = conn.QueryFirstOrDefault<Post>("SELECT * FROM posts WHERE id = @Id", new { Id = postId });
        if (post is null) return HttpResults.Error(404, "帖子不存在");
        var newStatus = body.Action switch
        {
            "approve" => 1,
            "reject" => 2,
            _ => -1,
        };
        if (newStatus < 0) return HttpResults.Error(400, "非法操作");
        var note = body.Note ?? "";
        if (note.Length > 255) note = note[..255];
        conn.Execute("UPDATE posts SET status = @S, review_note = @N WHERE id = @Id",
            new { S = newStatus, N = note, Id = postId });
        return Results.Json(new { ok = true });
    }

    private sealed class ReplyUserRow
    {
        public int Id { get; set; }
        public int? Uid { get; set; }
    }

    private sealed class ReplyAdminRow
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string Content { get; set; } = "";
        public int Status { get; set; }
        public bool IsAi { get; set; }
        public bool Crisis { get; set; }
        public bool IsAnonymous { get; set; }
        public string ReviewNote { get; set; } = "";
        public bool Recalled { get; set; }
        public string RecallReason { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? PostContent { get; set; }
    }

    private static IResult ListReplies(
        int? status, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var rows2 = status is null
            ? conn.Query<ReplyAdminRow>(
                "SELECT r.id AS Id, r.post_id AS PostId, r.content AS Content, r.status AS Status, " +
                "r.is_ai AS IsAi, r.crisis AS Crisis, r.is_anonymous AS IsAnonymous, " +
                "r.review_note AS ReviewNote, r.recalled AS Recalled, r.recall_reason AS RecallReason, " +
                "r.created_at AS CreatedAt, p.content AS PostContent " +
                "FROM replies r LEFT JOIN posts p ON p.id = r.post_id " +
                "ORDER BY r.created_at DESC LIMIT 300").ToList()
            : conn.Query<ReplyAdminRow>(
                "SELECT r.id AS Id, r.post_id AS PostId, r.content AS Content, r.status AS Status, " +
                "r.is_ai AS IsAi, r.crisis AS Crisis, r.is_anonymous AS IsAnonymous, " +
                "r.review_note AS ReviewNote, r.recalled AS Recalled, r.recall_reason AS RecallReason, " +
                "r.created_at AS CreatedAt, p.content AS PostContent " +
                "FROM replies r LEFT JOIN posts p ON p.id = r.post_id " +
                "WHERE r.status = @Status ORDER BY r.created_at DESC LIMIT 300",
                new { Status = status.Value }).ToList();

        var replyIds = rows2.Select(r => r.Id).ToList();
        var rawReplies = replyIds.Count == 0
            ? new Dictionary<int, int?>()
            : conn.Query<ReplyUserRow>(
                    "SELECT id AS Id, user_id AS Uid FROM replies WHERE id IN @Ids",
                    new { Ids = replyIds })
                .ToDictionary(r => r.Id, r => (int?)r.Uid);

        var out_ = rows2.Select(r =>
        {
            rawReplies.TryGetValue(r.Id, out var uid);
            var (author, phone) = RealAuthor(conn, uid);
            var excerpt = r.PostContent ?? "";
            var shown = excerpt.Length > 80 ? excerpt[..80] + "…" : excerpt;
            return new
            {
                id = r.Id,
                post_id = r.PostId,
                post_excerpt = shown,
                content = r.Content,
                status = r.Status,
                is_ai = r.IsAi,
                crisis = r.Crisis,
                is_anonymous = r.IsAnonymous,
                author,
                author_phone = phone,
                review_note = r.ReviewNote ?? "",
                recalled = r.Recalled,
                recall_reason = r.RecallReason ?? "",
                created_at = r.CreatedAt,
            };
        }).ToList();
        return Results.Json(out_);
    }

    private static IResult ModerateReply(
        int replyId, ModerateIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var reply = conn.QueryFirstOrDefault<Reply>("SELECT * FROM replies WHERE id = @Id", new { Id = replyId });
        if (reply is null) return HttpResults.Error(404, "回复不存在");
        var newStatus = body.Action switch
        {
            "approve" => 1,
            "reject" => 2,
            _ => -1,
        };
        if (newStatus < 0) return HttpResults.Error(400, "非法操作");
        var note = body.Note ?? "";
        if (note.Length > 255) note = note[..255];
        conn.Execute("UPDATE replies SET status = @S, review_note = @N WHERE id = @Id",
            new { S = newStatus, N = note, Id = replyId });
        return Results.Json(new { ok = true });
    }

    private static IResult RecallReply(
        int replyId, RecallIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var reply = conn.QueryFirstOrDefault<Reply>("SELECT * FROM replies WHERE id = @Id", new { Id = replyId });
        if (reply is null) return HttpResults.Error(404, "回复不存在");
        if (!reply.IsAi)
            return HttpResults.Error(400, "仅支持撤回 AI 回复；人类回复请使用驳回");
        var reason = (body.Reason ?? "").Trim();
        if (reason.Length == 0) return HttpResults.Error(400, "请填写撤回原因");
        if (reason.Length > 255) reason = reason[..255];
        conn.Execute("UPDATE replies SET recalled = 1, recall_reason = @R WHERE id = @Id",
            new { R = reason, Id = replyId });
        return Results.Json(new { ok = true });
    }

    private static IResult RestoreReply(
        int replyId, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var reply = conn.QueryFirstOrDefault<Reply>("SELECT * FROM replies WHERE id = @Id", new { Id = replyId });
        if (reply is null) return HttpResults.Error(404, "回复不存在");
        if (!reply.IsAi)
            return HttpResults.Error(400, "仅支持恢复 AI 回复");
        conn.Execute("UPDATE replies SET recalled = 0, recall_reason = '' WHERE id = @Id",
            new { Id = replyId });
        return Results.Json(new { ok = true });
    }

    private static IResult ListUsers(HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var list = conn.Query<User>("SELECT * FROM users ORDER BY created_at DESC")
            .Select(UserOut.From).ToList();
        return Results.Json(list);
    }

    private static IResult SetUserStatus(
        int userId, StatusIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var admin = RequireAdmin(ctx, users, out var err);
        if (admin is null) return err!;
        if (body.Status is not ("active" or "disabled"))
            return HttpResults.Error(400, "非法状态");
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (user is null) return HttpResults.Error(404, "用户不存在");
        if (user.Id == admin.Id && body.Status == "disabled")
            return HttpResults.Error(400, "不能停用当前登录的管理员");
        conn.Execute("UPDATE users SET status = @S WHERE id = @Id", new { S = body.Status, Id = userId });
        return Results.Json(new { ok = true });
    }

    private static IResult SetUserBadge(
        int userId, BadgeBody body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (user is null) return HttpResults.Error(404, "用户不存在");
        var badge = (body.Badge ?? "").Trim();
        if (badge.Length > 64) badge = badge[..64];
        conn.Execute("UPDATE users SET badge = @B WHERE id = @Id", new { B = badge, Id = userId });
        return Results.Json(new { ok = true, badge });
    }

    private static IResult SetUserRole(
        int userId, ActionBody body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var admin = RequireAdmin(ctx, users, out var err);
        if (admin is null) return err!;
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (user is null) return HttpResults.Error(404, "用户不存在");
        string newRole;
        if (body.Action == "promote")
        {
            if (user.Role == "admin") return HttpResults.Error(400, "该用户已是管理员");
            newRole = "admin";
        }
        else if (body.Action == "demote")
        {
            if (user.Role != "admin") return HttpResults.Error(400, "该用户不是管理员");
            if (user.Id == admin.Id)
                return HttpResults.Error(400, "不能取消自己的管理员权限");
            var adminCount = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users WHERE role = 'admin'");
            if (adminCount <= 1)
                return HttpResults.Error(400, "系统至少需要保留一个管理员");
            newRole = "user";
        }
        else
            return HttpResults.Error(400, "非法操作");
        conn.Execute("UPDATE users SET role = @R WHERE id = @Id", new { R = newRole, Id = userId });
        return Results.Json(new { ok = true, role = newRole });
    }

    private static IResult ResetUserPassword(
        int userId, PasswordBody body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var user = conn.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @Id", new { Id = userId });
        if (user is null) return HttpResults.Error(404, "用户不存在");
        var password = (body.Password ?? "").Trim();
        if (password.Length < 6) return HttpResults.Error(400, "密码至少 6 位");
        conn.Execute("UPDATE users SET password_hash = @P WHERE id = @Id",
            new { P = PasswordHasher.Hash(password), Id = userId });
        return Results.Json(new { ok = true });
    }

    private sealed class AiLogAdminRow
    {
        public int Id { get; set; }
        public string Module { get; set; } = "";
        public string Engine { get; set; } = "local";
        public string Prompt { get; set; } = "";
        public string Response { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public int? ReplyId { get; set; }
        public int? PostId { get; set; }
        public int? ReplyStatus { get; set; }
        public bool? Recalled { get; set; }
        public string? RecallReason { get; set; }
    }

    private static IResult ListAiLogs(HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (RequireAdmin(ctx, users, out var err) is null) return err!;
        using var conn = db.Open();
        var rows = conn.Query<AiLogAdminRow>(
            "SELECT l.id AS Id, l.module AS Module, l.engine AS Engine, l.prompt AS Prompt, " +
            "l.response AS Response, l.created_at AS CreatedAt, " +
            "r.id AS ReplyId, r.post_id AS PostId, r.status AS ReplyStatus, r.recalled AS Recalled, " +
            "r.recall_reason AS RecallReason " +
            "FROM ai_logs l LEFT JOIN replies r ON r.id = l.reply_id " +
            "ORDER BY l.created_at DESC LIMIT 200").ToList();

        var out_ = rows.Select(l => new
        {
            id = l.Id,
            module = l.Module,
            engine = l.Engine,
            prompt = l.Prompt.Length > 300 ? l.Prompt[..300] : l.Prompt,
            response = l.Response.Length > 600 ? l.Response[..600] : l.Response,
            created_at = l.CreatedAt,
            reply_id = l.ReplyId,
            post_id = l.PostId,
            reply_status = l.ReplyStatus,
            recalled = l.Recalled ?? false,
            recall_reason = l.RecallReason ?? "",
        }).ToList();
        return Results.Json(out_);
    }
}
