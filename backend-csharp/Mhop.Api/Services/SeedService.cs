using Dapper;
using Mhop.Data;
using Mhop.Security;

namespace Mhop.Services;

/// <summary>初始化种子数据（对应 Python seed.py）：admin/admin123 + 一条引导帖与 AI 回复。</summary>
public sealed class SeedService
{
    public const string WelcomePost =
        "最近压力很大，夜里总是睡不着，白天也提不起精神，不知道该怎么办……" +
        "第一次来这里，想问问大家都是怎么熬过低谷期的？";

    public const string WelcomeAiReply =
        "谢谢你愿意把这份疲惫说出来。失眠和情绪低落叠加时，人会格外消耗，先别苛责自己。\n\n" +
        "可以先尝试两件小事：\n" +
        "· 睡前1小时离开手机，做5分钟缓慢呼吸（吸气4秒-屏息7秒-呼气8秒），帮身体先放松；\n" +
        "· 白天安排一次10-20分钟的户外走动，自然光对睡眠节律很有帮助。\n\n" +
        "如果这种状态已经持续两周以上，或开始影响吃饭、工作和社交，建议到正规医院心理科做一次评估，" +
        "这不是软弱，而是认真照顾自己。我们一直在这里，你愿意多说一点也可以。";

    private readonly Database _db;

    public SeedService(Database db) => _db = db;

    public void EnsureSeed()
    {
        using var conn = _db.Open();
        var now = DateTime.Now;
        if (conn.QueryFirstOrDefault<int?>("SELECT id FROM users WHERE username = 'admin' LIMIT 1") is null)
        {
            conn.Execute(
                "INSERT INTO users (username, password_hash, role, status, avatar, badge, created_at) " +
                "VALUES (@Username, @Hash, 'admin', 'active', '', '', @Now)",
                new { Username = "admin", Hash = PasswordHasher.Hash("admin123"), Now = now });
        }

        if (conn.ExecuteScalar<long>("SELECT COUNT(*) FROM posts") == 0)
        {
            var postId = conn.ExecuteScalar<long>(
                "INSERT INTO posts (user_id, is_anonymous, content, board, images, status, crisis, view_count, review_note, created_at) " +
                "VALUES (NULL, 1, @Content, 'stress', '', 1, 0, 0, '', @Now); SELECT last_insert_rowid();",
                new { Content = WelcomePost, Now = now });
            conn.Execute(
                "INSERT INTO replies (post_id, user_id, is_anonymous, content, images, status, is_ai, crisis, review_note, recalled, recall_reason, created_at) " +
                "VALUES (@PostId, NULL, 1, @Content, '', 1, 1, 0, '', 0, '', @Now)",
                new { PostId = (int)postId, Content = WelcomeAiReply, Now = now });
        }
    }
}
