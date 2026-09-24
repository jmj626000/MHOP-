using System.Text.Json.Serialization;

namespace Mhop.Web;

// ---------------- 请求体（字段名与 Pydantic schema 对齐） ----------------

public sealed class RegisterIn
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public sealed class LoginIn
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}

public sealed class EmailCodeIn
{
    [JsonPropertyName("email")] public string Email { get; set; } = "";
}

public sealed class EmailLoginIn
{
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("code")] public string Code { get; set; } = "";
}

public sealed class ProfileUpdateIn
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("avatar")] public string Avatar { get; set; } = "";
}

public sealed class PhoneBindIn
{
    [JsonPropertyName("phone")] public string Phone { get; set; } = "";
}

public sealed class PostIn
{
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("is_anonymous")] public bool IsAnonymous { get; set; } = true;
    [JsonPropertyName("board")] public string Board { get; set; } = "";
    [JsonPropertyName("images")] public List<string> Images { get; set; } = [];
}

public sealed class ReplyIn
{
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("is_anonymous")] public bool IsAnonymous { get; set; } = true;
    [JsonPropertyName("images")] public List<string> Images { get; set; } = [];
}

public sealed class LikeIn
{
    [JsonPropertyName("target_type")] public string TargetType { get; set; } = "";
    [JsonPropertyName("target_id")] public int TargetId { get; set; }
}

public sealed class ModerateIn
{
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("note")] public string Note { get; set; } = "";
}

public sealed class RecallIn
{
    [JsonPropertyName("reason")] public string Reason { get; set; } = "";
}

public sealed class StatusIn
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}

public sealed class BadgeBody
{
    [JsonPropertyName("badge")] public string? Badge { get; set; }
}

public sealed class ActionBody
{
    [JsonPropertyName("action")] public string? Action { get; set; }
}

public sealed class PasswordBody
{
    [JsonPropertyName("password")] public string? Password { get; set; }
}

public sealed class AssessmentIn
{
    [JsonPropertyName("assessment_type")] public string AssessmentType { get; set; } = "";
    [JsonPropertyName("answers")] public Dictionary<string, int> Answers { get; set; } = new();
    [JsonPropertyName("free_text")] public string FreeText { get; set; } = "";
    [JsonPropertyName("save_to_cloud")] public bool SaveToCloud { get; set; }
}

public sealed class HeartbeatIn
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
}

// ---------------- 响应体 ----------------

public sealed class UserOut
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("phone")] public string? Phone { get; set; }
    [JsonPropertyName("role")] public string Role { get; set; } = "user";
    [JsonPropertyName("status")] public string Status { get; set; } = "active";
    [JsonPropertyName("avatar")] public string Avatar { get; set; } = "";
    [JsonPropertyName("badge")] public string Badge { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }

    public static UserOut From(Mhop.Data.User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        Phone = u.Phone,
        Role = u.Role,
        Status = u.Status,
        Avatar = u.Avatar,
        Badge = u.Badge,
        CreatedAt = u.CreatedAt,
    };
}

public sealed class TokenOut
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "bearer";
    [JsonPropertyName("new_account")] public bool NewAccount { get; set; }
    [JsonPropertyName("user")] public UserOut User { get; set; } = null!;
}

public sealed class ReplyOut
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("post_id")] public int PostId { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("is_ai")] public bool IsAi { get; set; }
    [JsonPropertyName("is_anonymous")] public bool IsAnonymous { get; set; }
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("author_avatar")] public string AuthorAvatar { get; set; } = "";
    [JsonPropertyName("author_badge")] public string AuthorBadge { get; set; } = "";
    [JsonPropertyName("crisis")] public bool Crisis { get; set; }
    [JsonPropertyName("recalled")] public bool Recalled { get; set; }
    [JsonPropertyName("recall_reason")] public string RecallReason { get; set; } = "";
    [JsonPropertyName("like_count")] public int LikeCount { get; set; }
    [JsonPropertyName("liked")] public bool Liked { get; set; }
    [JsonPropertyName("images")] public List<string> Images { get; set; } = [];
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
}

public class PostOut
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; } = "";
    [JsonPropertyName("board")] public string Board { get; set; } = "mood";
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("crisis")] public bool Crisis { get; set; }
    [JsonPropertyName("is_anonymous")] public bool IsAnonymous { get; set; }
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("author_avatar")] public string AuthorAvatar { get; set; } = "";
    [JsonPropertyName("author_badge")] public string AuthorBadge { get; set; } = "";
    [JsonPropertyName("reply_count")] public int ReplyCount { get; set; }
    [JsonPropertyName("view_count")] public int ViewCount { get; set; }
    [JsonPropertyName("ai_replied")] public bool AiReplied { get; set; }
    [JsonPropertyName("mine")] public bool Mine { get; set; }
    [JsonPropertyName("like_count")] public int LikeCount { get; set; }
    [JsonPropertyName("liked")] public bool Liked { get; set; }
    [JsonPropertyName("images")] public List<string> Images { get; set; } = [];
    [JsonPropertyName("last_reply_at")] public DateTime? LastReplyAt { get; set; }
    [JsonPropertyName("last_reply_author")] public string LastReplyAuthor { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
}

public sealed class PostDetailOut : PostOut
{
    [JsonPropertyName("replies")] public List<ReplyOut> Replies { get; set; } = [];
}

public sealed class AssessmentOut
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("assessment_type")] public string AssessmentType { get; set; } = "";
    [JsonPropertyName("score")] public int? Score { get; set; }
    [JsonPropertyName("level")] public string Level { get; set; } = "";
    [JsonPropertyName("level_code")] public string LevelCode { get; set; } = "";
    [JsonPropertyName("crisis")] public bool Crisis { get; set; }
    [JsonPropertyName("ai_result")] public string AiResult { get; set; } = "";
    [JsonPropertyName("saved_cloud")] public bool SavedCloud { get; set; }
    [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
}
