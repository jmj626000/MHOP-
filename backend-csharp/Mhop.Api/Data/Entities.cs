namespace Mhop.Data;

// 实体字段与 Python SQLAlchemy 模型一一对应；DateTime 统一为 naive 本地时间。

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "user";
    public string Status { get; set; } = "active";
    public string Avatar { get; set; } = "";
    public string Badge { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class Post
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public string Content { get; set; } = "";
    public string Board { get; set; } = "mood";
    public string Images { get; set; } = "";
    public int Status { get; set; }       // 0=待巡检 1=正常 2=违规隐藏
    public bool Crisis { get; set; }
    public int ViewCount { get; set; }
    public string ReviewNote { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class Reply
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int? UserId { get; set; }
    public bool IsAnonymous { get; set; } = true;
    public string Content { get; set; } = "";
    public string Images { get; set; } = "";
    public int Status { get; set; }       // 0=待审 1=通过 2=驳回
    public bool IsAi { get; set; }
    public bool Crisis { get; set; }
    public string ReviewNote { get; set; } = "";
    public bool Recalled { get; set; }
    public string RecallReason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class Like
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TargetType { get; set; } = ""; // post / reply
    public int TargetId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class Assessment
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string AssessmentType { get; set; } = "";
    public string InputData { get; set; } = "";
    public string AiResult { get; set; } = "";
    public int? Score { get; set; }
    public string Level { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AiLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string SessionId { get; set; } = "";
    public string Module { get; set; } = "";
    public int? ReplyId { get; set; }
    public string Prompt { get; set; } = "";
    public string Response { get; set; } = "";
    public string Engine { get; set; } = "local";
    public DateTime CreatedAt { get; set; }
}
