using System.Text.Json.Serialization;

namespace Mhop.Services;

public sealed record BoardDef(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("desc")] string Desc);

/// <summary>论坛板块（对应 Python boards.py）。</summary>
public static class Boards
{
    public static readonly IReadOnlyList<BoardDef> All =
    [
        new("crisis", "危机求助", "#e0524d", "出现自伤/自杀念头或紧急风险，请优先拨打援助热线"),
        new("mood", "情绪树洞", "#2f8f83", "抑郁、焦虑、低落、崩溃，想找个安全的地方说说"),
        new("stress", "压力倾诉", "#e8933c", "学业、工作、家庭与生活压力"),
        new("relation", "人际情感", "#d6668f", "亲情、友情、爱情等人际关系困扰"),
        new("sleep", "睡眠困扰", "#6a6fd0", "失眠、多梦、作息紊乱与精神疲惫"),
        new("recovery", "康复经验", "#4e9e5f", "好转经历与自我调节方法分享"),
        new("chat", "随便聊聊", "#8a8f99", "不属于以上板块，只想找人说说话"),
    ];

    public static readonly HashSet<string> Slugs = All.Select(b => b.Slug).ToHashSet();
}
