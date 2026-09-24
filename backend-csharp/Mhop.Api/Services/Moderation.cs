namespace Mhop.Services;

/// <summary>内容安全：危机词识别 + 敏感词巡检（与 Python moderation.py 同步）。</summary>
public static class Moderation
{
    public static readonly string[] CrisisWords =
    [
        "自杀", "自尽", "轻生", "不想活", "活着没意思", "活不下去", "活够了",
        "结束生命", "离开这个世界", "一了百了", "找死", "寻死", "同归于尽",
        "自残", "割腕", "割自己", "跳楼", "上吊", "烧炭", "安眠药", "伤害自己",
        "毁掉自己", "撑不下去了", "解脱",
    ];

    public static readonly string[] SensitiveWords =
    [
        "色情", "裸体", "约炮", "迷奸", "冰毒", "海洛因", "卖毒品", "枪支",
        "炸药", "赌博网站", "刷单", "代开发票", "私家侦探", "高利贷",
        "加微信领", "点击链接", "六合彩", "代考",
    ];

    public static bool DetectCrisis(string text)
    {
        foreach (var w in CrisisWords)
            if (text.Contains(w, StringComparison.Ordinal)) return true;
        return false;
    }

    public static List<string> HitSensitive(string text) =>
        SensitiveWords.Where(w => text.Contains(w, StringComparison.Ordinal)).ToList();
}
