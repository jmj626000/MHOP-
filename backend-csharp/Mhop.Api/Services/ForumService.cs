using System.Text.Json;
using Dapper;
using Mhop.Data;
using Mhop.Web;

namespace Mhop.Services;

/// <summary>论坛输出组装与点赞统计（对应 forum.py 中的 _author_for / _post_out / _reply_out 等）。</summary>
public sealed class ForumService
{
    private readonly Database _db;

    public ForumService(Database db) => _db = db;

    public static List<string> ParseImages(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Take(9)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public sealed class UserBrief
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Avatar { get; set; } = "";
        public string Badge { get; set; } = "";
    }

    public static Dictionary<int, UserBrief> LoadUsersByIds(System.Data.IDbConnection conn, IEnumerable<int?> ids)
    {
        var clean = ids.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        if (clean.Count == 0) return [];
        return conn.Query<UserBrief>(
            "SELECT id AS Id, username AS Username, avatar AS Avatar, badge AS Badge FROM users WHERE id IN @Ids",
            new { Ids = clean }).ToDictionary(u => u.Id);
    }

    private static Dictionary<int, UserBrief> LoadUsers(System.Data.IDbConnection conn, IEnumerable<int?> ids) =>
        LoadUsersByIds(conn, ids);

    private static (string Name, string Avatar, string Badge) AuthorFor(
        bool isAi, bool isAnonymous, int? userId, IReadOnlyDictionary<int, UserBrief> users)
    {
        if (isAi) return ("AI 心理助手", "", "");
        if (!isAnonymous && userId is not null)
        {
            if (users.TryGetValue(userId.Value, out var u))
                return (u.Username, u.Avatar ?? "", u.Badge ?? "");
            return ("实名用户", "", "");
        }
        return ("匿名朋友", "", "");
    }

    public Dictionary<int, int> LikeCountMap(System.Data.IDbConnection conn, string targetType, IReadOnlyList<int> ids)
    {
        if (ids.Count == 0) return [];
        return conn.Query<LikeCountRow>(
            "SELECT target_id AS TargetId, COUNT(*) AS Cnt FROM likes " +
            "WHERE target_type = @T AND target_id IN @Ids GROUP BY target_id",
            new { T = targetType, Ids = ids })
            .ToDictionary(r => r.TargetId, r => r.Cnt);
    }

    private sealed class LikeCountRow
    {
        public int TargetId { get; set; }
        public int Cnt { get; set; }
    }

    public HashSet<int> LikedSet(System.Data.IDbConnection conn, User? user, string targetType, IReadOnlyList<int> ids)
    {
        if (user is null || ids.Count == 0) return [];
        return conn.Query<int>(
            "SELECT target_id FROM likes WHERE user_id = @Uid AND target_type = @T AND target_id IN @Ids",
            new { Uid = user.Id, T = targetType, Ids = ids }).ToHashSet();
    }

    public ReplyOut BuildReply(Reply r, IReadOnlyDictionary<int, UserBrief> users,
        Dictionary<int, int> counts, HashSet<int> liked)
    {
        var recalled = r.Recalled;
        var (author, avatar, badge) = AuthorFor(r.IsAi, r.IsAnonymous, r.UserId, users);
        return new ReplyOut
        {
            Id = r.Id,
            PostId = r.PostId,
            Content = recalled ? "" : r.Content,
            Status = r.Status,
            IsAi = r.IsAi,
            IsAnonymous = r.IsAnonymous,
            Author = author,
            AuthorAvatar = avatar,
            AuthorBadge = badge,
            Crisis = r.Crisis,
            Recalled = recalled,
            RecallReason = r.RecallReason ?? "",
            LikeCount = counts.GetValueOrDefault(r.Id),
            Liked = liked.Contains(r.Id),
            Images = ParseImages(r.Images),
            CreatedAt = r.CreatedAt,
        };
    }

    public PostOut BuildPost(Post p, System.Data.IDbConnection conn, User? current,
        IReadOnlyList<Reply> visibleReplies,
        Dictionary<int, int>? postLikeCounts = null, HashSet<int>? postLiked = null,
        Dictionary<int, UserBrief>? preloadedUsers = null)
    {
        var userIds = new List<int?>();
        if (!p.IsAnonymous) userIds.Add(p.UserId);
        foreach (var r in visibleReplies)
            if (!r.IsAnonymous) userIds.Add(r.UserId);
        var users = preloadedUsers ?? LoadUsers(conn, userIds);

        Reply? last = null;
        foreach (var r in visibleReplies)
            if (last is null || r.CreatedAt > last.CreatedAt) last = r;

        var (author, avatar, badge) = AuthorFor(false, p.IsAnonymous, p.UserId, users);
        string lastAuthor = "";
        if (last is not null)
            (lastAuthor, _, _) = AuthorFor(last.IsAi, last.IsAnonymous, last.UserId, users);

        return new PostOut
        {
            Id = p.Id,
            Content = p.Content,
            Board = p.Board,
            Status = p.Status,
            Crisis = p.Crisis,
            IsAnonymous = p.IsAnonymous,
            Author = author,
            AuthorAvatar = avatar,
            AuthorBadge = badge,
            ReplyCount = visibleReplies.Count,
            ViewCount = p.ViewCount,
            AiReplied = visibleReplies.Any(r => r.IsAi),
            Mine = current is not null && p.UserId == current.Id,
            LikeCount = (postLikeCounts ?? []).GetValueOrDefault(p.Id),
            Liked = (postLiked ?? []).Contains(p.Id),
            Images = ParseImages(p.Images),
            LastReplyAt = last?.CreatedAt,
            LastReplyAuthor = lastAuthor,
            CreatedAt = p.CreatedAt,
        };
    }

    /// <summary>取一批帖子的“可见回复”（status=1 且未撤回）。</summary>
    public static List<Reply> VisibleReplies(System.Data.IDbConnection conn, IReadOnlyList<int> postIds)
    {
        if (postIds.Count == 0) return [];
        return conn.Query<Reply>(
            "SELECT * FROM replies WHERE post_id IN @Ids AND status = 1 AND recalled = 0",
            new { Ids = postIds }).ToList();
    }

    /// <summary>帖子详情页：status=1 的全部回复（含已撤回 AI 回复占位），AI 优先、组内按时间升序。</summary>
    public static List<Reply> DetailRepliesOrdered(System.Data.IDbConnection conn, int postId) =>
        conn.Query<Reply>(
            "SELECT * FROM replies WHERE post_id = @Pid AND status = 1 ORDER BY (CASE WHEN is_ai = 1 THEN 0 ELSE 1 END), created_at",
            new { Pid = postId }).ToList();

    public Database Db => _db;
}
