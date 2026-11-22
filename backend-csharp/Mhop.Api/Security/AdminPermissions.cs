using System.Text.Json;
using Mhop.Data;

namespace Mhop.Security;

/// <summary>
/// 后台模块级权限：与前端后台四个菜单一一对应。
/// superadmin 隐式拥有全部权限且可分配权限；普通 admin 仅拥有 permissions 字段中显式授予的模块。
/// </summary>
public static class AdminPermissions
{
    public const string Dashboard = "dashboard"; // 数据看板
    public const string Review = "review";       // 内容审核（帖子/回复/AI 回复管理）
    public const string Users = "users";         // 用户管理
    public const string AiLogs = "ai_logs";      // AI 交互日志
    public const string Announcements = "announcements"; // 公告管理

    public static readonly string[] All = [Dashboard, Review, Users, AiLogs, Announcements];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Dashboard] = "数据看板",
        [Review] = "内容审核",
        [Users] = "用户管理",
        [AiLogs] = "AI 交互日志",
        [Announcements] = "公告管理",
    };

    public static bool IsStaff(string? role) => role is "admin" or "superadmin";

    public static bool IsSuper(string? role) => role == "superadmin";

    /// <summary>解析用户存储的权限码 JSON 数组，过滤掉非法/未知权限码。</summary>
    public static List<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var codes = JsonSerializer.Deserialize<List<string>>(json);
            if (codes is null) return [];
            return codes.Where(All.Contains).Distinct().ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<string> codes) =>
        JsonSerializer.Serialize(codes.Where(All.Contains).Distinct().ToList());

    /// <summary>该用户是否拥有某模块权限（每次请求实时读库，权限调整即时生效）。</summary>
    public static bool Has(User? user, string code)
    {
        if (user is null || !IsStaff(user.Role)) return false;
        if (user.Role == "superadmin") return true;
        return Parse(user.Permissions).Contains(code);
    }

    /// <summary>返回用户的有效权限码列表（superadmin 返回全量），用于下发给前端。</summary>
    public static List<string> Effective(User? user)
    {
        if (user is null || !IsStaff(user.Role)) return [];
        return user.Role == "superadmin" ? [.. All] : Parse(user.Permissions);
    }
}
