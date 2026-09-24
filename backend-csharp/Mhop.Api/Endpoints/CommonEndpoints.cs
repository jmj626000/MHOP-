using Mhop.Configuration;
using Mhop.Services;
using Mhop.Web;

namespace Mhop.Endpoints;

public static class CommonEndpoints
{
    public static void MapCommon(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", (AppSettings s) => Results.Json(new { status = "ok", app = s.AppName }));
        app.MapGet("/api/hotlines", (AppSettings s) => Results.Json(BuildHotlines(s)));
        app.MapPost("/api/online/heartbeat", (HeartbeatIn body, OnlineTracker online) =>
            Results.Json(new { online = online.Heartbeat(string.IsNullOrEmpty(body.Key) ? "anon" : (body.Key.Length > 64 ? body.Key[..64] : body.Key)) }));
        app.MapGet("/api/online/count", (OnlineTracker online) => Results.Json(new { online = online.Count() }));
    }

    /// <summary>对应 Python config.HOTLINES：3 条固定热线 + 可选的乐科专线。</summary>
    private static List<Hotline> BuildHotlines(AppSettings s)
    {
        var list = new List<Hotline>
        {
            new("全国心理援助热线", "12356", "全国通用 · 24小时", "国家卫健委统一心理援助热线，免费、保密", "national"),
            new("北京心理危机研究与干预中心", "010-82951332", "危机干预 · 24小时",
                "面向自杀/自伤等紧急心理危机的专业干预热线", "national"),
            new("紧急报警 / 医疗急救", "110 / 120", "生命受到直接威胁时",
                "若你或身边人正处在立即危险中，请第一时间拨打", "emergency"),
        };
        if (!string.IsNullOrEmpty(s.LekeHotline))
            list.Add(new Hotline(
                "北京乐科心理干预研究院专属援助电话",
                s.LekeHotline, "合作机构专线", "平台合作研究院专属援助通道", "partner"));
        return list;
    }
}
