using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mhop.Data;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class ForumEndpoints
{
    public static void MapForum(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/forum");

        g.MapGet("/boards", GetBoards);
        g.MapGet("/stats", Stats);
        g.MapGet("/posts", ListPosts);
        g.MapPost("/posts", CreatePost);
        g.MapGet("/posts/{postId:int}", GetPost);
        g.MapPost("/posts/{postId:int}/replies", CreateReply);
        g.MapPost("/likes/toggle", ToggleLike);
        g.MapGet("/likes/mine", MyLikes);
    }

    private static IResult GetBoards(Database db, ForumService svc)
    {
        using var conn = db.Open();
        var counts = conn.Query<BoardCount>(
            "SELECT board AS Slug, COUNT(*) AS Cnt FROM posts WHERE status = 1 GROUP BY board")
            .ToDictionary(r => r.Slug, r => r.Cnt);
        var items = Boards.All.Select(b => new
        {
            slug = b.Slug,
            name = b.Name,
            color = b.Color,
            desc = b.Desc,
            count = counts.GetValueOrDefault(b.Slug),
        });
        return Results.Json(items);
    }

    private sealed class BoardCount
    {
        public string Slug { get; set; } = "";
        public int Cnt { get; set; }
    }

    private static IResult Stats(Database db, OnlineTracker online)
    {
        using var conn = db.Open();
        return Results.Json(new
        {
            posts = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM posts WHERE status = 1"),
            replies = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM replies WHERE status = 1"),
            users = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM users"),
            online = online.Count(),
        });
    }

    private static IResult ListPosts(
        int? page, int? size, string? keyword, string? board, string? sort,
        HttpContext ctx, Database db, ForumService svc, CurrentUserProvider users)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var sz = size switch
        {
            null => 10,
            < 1 => 10,
            > 50 => 50,
            _ => size.Value,
        };

        using var conn = db.Open();
        var where = "WHERE status = 1";
        var kw = (keyword ?? "").Trim();
        if (kw.Length > 0)
            where += " AND content LIKE @Kw";
        string? boardFilter = null;
        if (!string.IsNullOrEmpty(board))
        {
            if (!Boards.Slugs.Contains(board))
                return HttpResults.Error(400, "板块不存在");
            where += " AND board = @Board";
            boardFilter = board;
        }

        var order = sort == "new"
            ? "created_at DESC"
            : "COALESCE((SELECT MAX(created_at) FROM replies r WHERE r.post_id = posts.id " +
              "AND r.status = 1 AND r.recalled = 0), created_at) DESC";

        var queryParam = new
        {
            Kw = kw.Length > 0 ? $"%{kw}%" : null,
            Board = boardFilter,
            Limit = sz,
            Offset = (p - 1) * sz,
        };
        var total = conn.ExecuteScalar<long>($"SELECT COUNT(*) FROM posts {where}", queryParam);
        var posts = conn.Query<Post>(
            $"SELECT * FROM posts {where} ORDER BY {order} LIMIT @Limit OFFSET @Offset",
            queryParam).ToList();

        var ids = posts.Select(x => x.Id).ToList();
        var counts = svc.LikeCountMap(conn, "post", ids);
        var current = users.Optional(ctx);
        var liked = svc.LikedSet(conn, current, "post", ids);

        var allReplies = ForumService.VisibleReplies(conn, ids);
        var repliesByPost = allReplies.GroupBy(r => r.PostId).ToDictionary(g2 => g2.Key, g2 => g2.ToList());
        var userIds = posts.Where(x => !x.IsAnonymous).Select(x => x.UserId)
            .Concat(allReplies.Where(r => !r.IsAnonymous).Select(r => r.UserId)).ToList();
        var userMap = ForumService.LoadUsersByIds(conn, userIds!);

        var items = posts.Select(post =>
            svc.BuildPost(post, conn, current,
                repliesByPost.GetValueOrDefault(post.Id, []), counts, liked, userMap)).ToList();
        return Results.Json(new { total, page = p, size = sz, items });
    }

    private static IResult CreatePost(
        PostIn body, HttpContext ctx, Database db, ForumService svc,
        CurrentUserProvider users, AiService ai)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");
        if (string.IsNullOrEmpty(current.Phone))
            return HttpResults.Error(403, "发帖前请先在个人主页绑定手机号");

        var content = body.Content.Trim();
        if (content.Length == 0) return HttpResults.Error(400, "内容不能为空");
        if (!Boards.Slugs.Contains(body.Board)) return HttpResults.Error(400, "请选择板块");

        var crisis = Moderation.DetectCrisis(content);
        var images = body.Images.Count == 0 ? "" : JsonSerializer.Serialize(body.Images.Take(9));

        using var conn = db.Open();
        var now = DateTime.Now;
        var id = conn.ExecuteScalar<long>(
            "INSERT INTO posts (user_id, is_anonymous, content, board, images, status, crisis, view_count, review_note, created_at) " +
            "VALUES (@UserId, @IsAnon, @Content, @Board, @Images, 0, @Crisis, 0, '', @Now); SELECT last_insert_rowid();",
            new
            {
                UserId = current.Id,
                IsAnon = body.IsAnonymous ? 1 : 0,
                Content = content,
                body.Board,
                Images = images,
                Crisis = crisis ? 1 : 0,
                Now = now,
            });

        var post = conn.QueryFirst<Post>("SELECT * FROM posts WHERE id = @Id", new { Id = (int)id });
        var out_ = svc.BuildPost(post, conn, current, [], postLikeCounts: null, postLiked: null);

        // 异步触发 AI 自动回复，不阻塞发帖请求（对应 FastAPI BackgroundTasks）
        var postId = (int)id;
        _ = Task.Run(async () =>
        {
            try
            {
                var (text, engine) = await ai.ForumReplyAsync(content, crisis);
                using var c = db.Open();
                var replyId = c.ExecuteScalar<long>(
                    "INSERT INTO replies (post_id, user_id, is_anonymous, content, images, status, is_ai, crisis, review_note, recalled, recall_reason, created_at) " +
                    "VALUES (@PostId, NULL, 1, @Content, '', 1, 1, @Crisis, '', 0, '', @Now); SELECT last_insert_rowid();",
                    new { PostId = postId, Content = text, Crisis = crisis ? 1 : 0, Now = DateTime.Now });
                c.Execute(
                    "INSERT INTO ai_logs (user_id, session_id, module, reply_id, prompt, response, engine, created_at) " +
                    "VALUES (NULL, '', 'forum', @ReplyId, @Prompt, @Response, @Engine, @Now)",
                    new
                    {
                        ReplyId = (int)replyId,
                        Prompt = content.Length > 2000 ? content[..2000] : content,
                        Response = text.Length > 4000 ? text[..4000] : text,
                        Engine = engine,
                        Now = DateTime.Now,
                    });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AI 自动回复失败] post={postId}: {ex.Message}");
            }
        });

        return Results.Json(out_, statusCode: 201);
    }

    private static IResult GetPost(
        int postId, [FromQuery(Name = "inc_view")] bool? incView, HttpContext ctx,
        Database db, ForumService svc, CurrentUserProvider users)
    {
        using var conn = db.Open();
        var post = conn.QueryFirstOrDefault<Post>("SELECT * FROM posts WHERE id = @Id", new { Id = postId });
        if (post is null || post.Status != 1)
            return HttpResults.Error(404, "帖子不存在或正在审核中");
        if (incView == true)
        {
            conn.Execute("UPDATE posts SET view_count = view_count + 1 WHERE id = @Id", new { Id = postId });
            post.ViewCount += 1;
        }

        var current = users.Optional(ctx);
        var postCounts = svc.LikeCountMap(conn, "post", [postId]);
        var postLiked = svc.LikedSet(conn, current, "post", [postId]);
        var replies = ForumService.DetailRepliesOrdered(conn, postId);
        var replyIds = replies.Select(r => r.Id).ToList();
        var counts = svc.LikeCountMap(conn, "reply", replyIds);
        var liked = svc.LikedSet(conn, current, "reply", replyIds);
        var visible = replies.Where(r => r.Status == 1 && !r.Recalled).ToList();
        var userIds = replies.Where(r => !r.IsAnonymous).Select(r => r.UserId).ToList();
        if (!post.IsAnonymous) userIds.Add(post.UserId);
        var userMap = ForumService.LoadUsersByIds(conn, userIds);

        var baseOut = svc.BuildPost(post, conn, current, visible, postCounts, postLiked, userMap);
        var detail = new PostDetailOut
        {
            Id = baseOut.Id,
            Content = baseOut.Content,
            Board = baseOut.Board,
            Status = baseOut.Status,
            Crisis = baseOut.Crisis,
            IsAnonymous = baseOut.IsAnonymous,
            Author = baseOut.Author,
            AuthorAvatar = baseOut.AuthorAvatar,
            AuthorBadge = baseOut.AuthorBadge,
            ReplyCount = baseOut.ReplyCount,
            ViewCount = baseOut.ViewCount,
            AiReplied = baseOut.AiReplied,
            Mine = baseOut.Mine,
            LikeCount = baseOut.LikeCount,
            Liked = baseOut.Liked,
            Images = baseOut.Images,
            LastReplyAt = baseOut.LastReplyAt,
            LastReplyAuthor = baseOut.LastReplyAuthor,
            CreatedAt = baseOut.CreatedAt,
            Replies = replies.Select(r => svc.BuildReply(r, userMap, counts, liked)).ToList(),
        };
        return Results.Json(detail);
    }

    private static IResult CreateReply(
        int postId, ReplyIn body, HttpContext ctx, Database db, ForumService svc, CurrentUserProvider users)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");
        if (string.IsNullOrEmpty(current.Phone))
            return HttpResults.Error(403, "发帖前请先在个人主页绑定手机号");

        using var conn = db.Open();
        var post = conn.QueryFirstOrDefault<Post>("SELECT * FROM posts WHERE id = @Id", new { Id = postId });
        if (post is null || post.Status == 2)
            return HttpResults.Error(404, "帖子不存在或已被移除");
        var content = body.Content.Trim();
        if (content.Length == 0) return HttpResults.Error(400, "回复内容不能为空");

        var crisis = Moderation.DetectCrisis(content);
        var words = Moderation.HitSensitive(content);
        var status = words.Count > 0 ? 2 : 0;
        var note = words.Count > 0 ? "系统拦截：命中敏感词 " + string.Join(",", words) : "";
        var images = body.Images.Count == 0 ? "" : JsonSerializer.Serialize(body.Images.Take(9));

        var id = conn.ExecuteScalar<long>(
            "INSERT INTO replies (post_id, user_id, is_anonymous, content, images, status, is_ai, crisis, review_note, recalled, recall_reason, created_at) " +
            "VALUES (@PostId, @UserId, @IsAnon, @Content, @Images, @Status, 0, @Crisis, @Note, 0, '', @Now); SELECT last_insert_rowid();",
            new
            {
                PostId = postId,
                UserId = current.Id,
                IsAnon = body.IsAnonymous ? 1 : 0,
                Content = content,
                Images = images,
                Status = status,
                Crisis = crisis ? 1 : 0,
                Note = note.Length > 255 ? note[..255] : note,
                Now = DateTime.Now,
            });
        var reply = conn.QueryFirst<Reply>("SELECT * FROM replies WHERE id = @Id", new { Id = (int)id });
        var userMap = ForumService.LoadUsersByIds(conn, [reply.UserId]);
        return Results.Json(svc.BuildReply(reply, userMap, [], []), statusCode: 201);
    }

    private static IResult ToggleLike(
        LikeIn body, HttpContext ctx, Database db, CurrentUserProvider users)
    {
        var current = users.Required(ctx);
        if (current is null) return HttpResults.Error(401, "请先登录");
        if (body.TargetType is not ("post" or "reply"))
            return HttpResults.Error(400, "非法点赞对象");

        using var conn = db.Open();
        if (body.TargetType == "post")
        {
            var p = conn.QueryFirstOrDefault<Post>("SELECT * FROM posts WHERE id = @Id", new { Id = body.TargetId });
            if (p is null || p.Status == 2)
                return HttpResults.Error(404, "内容不存在或已被移除");
        }
        else
        {
            var r = conn.QueryFirstOrDefault<Reply>("SELECT * FROM replies WHERE id = @Id", new { Id = body.TargetId });
            if (r is null || r.Status == 2)
                return HttpResults.Error(404, "内容不存在或已被移除");
            if (r.Status != 1 || r.Recalled)
                return HttpResults.Error(400, "该回复暂不可点赞");
        }

        var existing = conn.QueryFirstOrDefault<Like>(
            "SELECT * FROM likes WHERE user_id = @Uid AND target_type = @T AND target_id = @Tid",
            new { Uid = current.Id, T = body.TargetType, Tid = body.TargetId });
        bool liked;
        if (existing is not null)
        {
            conn.Execute("DELETE FROM likes WHERE id = @Id", new { existing.Id });
            liked = false;
        }
        else
        {
            conn.Execute(
                "INSERT INTO likes (user_id, target_type, target_id, created_at) VALUES (@Uid, @T, @Tid, @Now)",
                new { Uid = current.Id, T = body.TargetType, Tid = body.TargetId, Now = DateTime.Now });
            liked = true;
        }
        var count = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM likes WHERE target_type = @T AND target_id = @Tid",
            new { T = body.TargetType, Tid = body.TargetId });
        return Results.Json(new { liked, like_count = count });
    }

    private static IResult MyLikes([FromQuery(Name = "target_type")] string? targetType,
        HttpContext ctx, Database db, CurrentUserProvider users)
    {
        if (targetType is not ("post" or "reply"))
            return HttpResults.Error(422, "非法点赞对象");
        var current = users.Optional(ctx);
        if (current is null) return Results.Json(new { ids = Array.Empty<int>() });
        using var conn = db.Open();
        var ids = conn.Query<int>(
            "SELECT target_id FROM likes WHERE user_id = @Uid AND target_type = @T",
            new { Uid = current.Id, T = targetType }).ToList();
        return Results.Json(new { ids });
    }
}
