using System.Text.Json.Serialization;

namespace Mhop.Services;

public sealed record ScaleOption(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("value")] int Value);

public sealed record ScaleBand(int Ceiling, string Label, string Code);

public sealed class ScaleDef
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Intro { get; init; }
    public required int Max { get; init; }
    public required List<ScaleOption> Options { get; init; }
    public required List<string> Questions { get; init; }
    public required List<ScaleBand> Bands { get; init; }
    public int? CrisisIndex { get; init; }
}

/// <summary>PHQ-9 / GAD-7 标准量表（对应 Python scales.py）。</summary>
public static class Scales
{
    private static readonly List<ScaleOption> Options =
    [
        new("完全不会", 0),
        new("有几天", 1),
        new("一半以上的天数", 2),
        new("几乎每天", 3),
    ];

    public static readonly Dictionary<string, ScaleDef> All = new()
    {
        ["phq9"] = new ScaleDef
        {
            Key = "phq9",
            Name = "PHQ-9 抑郁症筛查量表",
            Intro = "根据过去两周的状况作答。PHQ-9 是国际通用的抑郁症状筛查工具，结果仅供自我了解，不构成诊断。",
            Max = 27,
            Options = Options,
            Questions =
            [
                "做事时提不起劲或没有兴趣",
                "感到心情低落、沮丧或绝望",
                "入睡困难、睡不安稳或睡眠过多",
                "感觉疲倦或没有活力",
                "食欲不振或吃得太多",
                "觉得自己很糟——或觉得自己很失败，或让自己/家人失望",
                "对事物专注有困难，例如阅读或看电视时",
                "动作或说话速度缓慢到别人已察觉？或相反——比平常更加烦躁、坐立不安",
                "有不如死掉或用某种方式伤害自己的念头",
            ],
            Bands =
            [
                new(4, "无明显症状", "normal"),
                new(9, "轻度", "mild"),
                new(14, "中度", "moderate"),
                new(19, "中重度", "severe"),
                new(27, "重度", "danger"),
            ],
            CrisisIndex = 8,
        },
        ["gad7"] = new ScaleDef
        {
            Key = "gad7",
            Name = "GAD-7 广泛性焦虑筛查量表",
            Intro = "根据过去两周的状况作答。GAD-7 是国际通用的焦虑症状筛查工具，结果仅供自我了解，不构成诊断。",
            Max = 21,
            Options = Options,
            Questions =
            [
                "感到紧张、焦虑或急躁",
                "不能停止或控制担忧",
                "对各种各样的事情担忧过多",
                "很难放松下来",
                "由于不安而无法静坐",
                "变得容易烦恼或急躁",
                "感到似乎将有可怕的事情发生而害怕",
            ],
            Bands =
            [
                new(4, "无明显症状", "normal"),
                new(9, "轻度", "mild"),
                new(14, "中度", "moderate"),
                new(21, "重度", "danger"),
            ],
            CrisisIndex = null,
        },
    };

    /// <summary>计分分级（bands 按 ceiling 升序匹配）。</summary>
    public static (string Label, string Code) ScoreBand(string key, int score)
    {
        foreach (var b in All[key].Bands)
            if (score <= b.Ceiling) return (b.Label, b.Code);
        var last = All[key].Bands[^1];
        return (last.Label, last.Code);
    }
}
