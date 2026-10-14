using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mhop.Configuration;

namespace Mhop.Services;

public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// AI 适配层（对应 Python ai.py）：
/// 配置 LLM_BASE_URL + LLM_API_KEY 时走 OpenAI 兼容 Chat Completions（讯飞星火等）；
/// 失败或未配置时降级到内置共情式规则回复；危机内容强制前置援助热线。
/// </summary>
public sealed class AiService
{
    public const string CrisisPrefix =
        "亲爱的，我感觉到你此刻真的很难受很难受，心里好像被什么东西压得喘不过气对不对？我在呢，不会让你一个人扛着。\n" +
        "请一定先把安全放在第一位：\n" +
        "1）拨打全国心理援助热线 12356（24小时、免费、保密），或者北京心理危机干预中心 010-82951332，那里有专业的姐姐愿意听你说；\n" +
        "2）如果你觉得自己可能马上要伤害自己，请立刻打 110 或 120，或者直接去最近医院的急诊；\n" +
        "3）现在就给一个你信得过的家人或发条消息吧，说一句'我现在不太好，可以陪陪我吗'，然后把身边危险的东西先放远一点。\n\n" +
        "你值得被好好对待，我在这里等你。";

    private const string ForumSystemPrompt =
        "你是心光平台的软萌AI陪伴者'小心光'，一个粉发蓝裙、抱着温暖心形光晕的少女。" +
        "说话风格：亲切软萌、像朋友一样、偶尔用可爱的语气词（呢、啦、嗯呀），" +
        "但在共情和建议部分保持真诚可靠，不油腻。\n" +
        "要求：\n" +
        "1. 先认真接住用户的情绪（比如说'抱抱你，听起来真的好委屈'），" +
        "再给1-3条具体、好操作的小建议；\n" +
        "2. 不做医学诊断，不说'一切都会好起来的'这种空安慰，" +
        "也不使用空洞说教；\n" +
        "3. 回复控制在280字以内；\n" +
        "4. 如果用户提到自伤、自杀、不想活等危机信号，" +
        "必须在回复最前面温柔但明确地给出全国心理援助热线12356、" +
        "北京心理危机干预中心010-82951332，并建议立即联系身边人或拨打110/120；\n" +
        "5. 不讨论任何伤害自己的具体方法。";

    private static readonly string[] FallbackOpenings =
    [
        "谢谢你愿意跟我说这些呀，能把心里的话说出来就已经很勇敢了，抱抱你～",
        "我能感觉到你现在一定累坏了，你所有的感受都是真的，都值得被认真听听。",
        "隔着屏幕给你一个暖暖的陪伴——你不用一个人扛着这些的，我在这儿呢。",
        "呀…听起来真的好难好难，辛苦了，先让我陪陪你吧。",
    ];

    private static readonly string[] FallbackTips =
    [
        "试着做一个'着陆小练习'哦：慢慢说出你眼前能看到的5样东西、听到的4种声音、身体碰到的3样物品，把注意力轻轻拉回当下～",
        "试试4-7-8小呼吸：吸4秒、屏7秒、慢慢呼8秒，来3-4轮，帮你的小神经先安定下来一点。",
        "把脑子里的想法随便写在纸上吧，分成'我感受到的情绪'和'我要解决的问题'两栏——情绪先抱抱它，问题可以拆成最小的一小步慢慢来。",
        "今天只给自己定一个超级小的目标好吗？比如好好吃一顿饭、出门走10分钟、喝一杯热饮，做到就是赢啦。",
        "晚上情绪特别重的话，提前给自己准备一个'急救小包包'：温热的饮品、能联系的人、一段让你安心的音乐，不舒服的时候就掏出来用。",
        "情绪像海浪一样，最汹涌的峰值通常20-40分钟就会回落啦。在最难受的那一刻，先不要急着做任何决定，只让自己安全地度过这一小波就好。",
    ];

    private const string FallbackEnding =
        "\n\n这里是匿名、安全的空间，你可以继续多说一点。" +
        "如果这些困扰已经持续两周以上、明显影响睡眠、饮食或日常功能，建议到正规医院心理科/精神科做一次评估——" +
        "寻求专业帮助是力量，而不是软弱。";

    private readonly AppSettings _settings;
    private readonly HttpClient _http;

    public AiService(AppSettings settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>返回 (回复文本, 引擎标识 llm/local)。</summary>
    public async Task<(string Text, string Engine)> ChatAsync(IReadOnlyList<ChatMessage> messages)
    {
        if (!string.IsNullOrEmpty(_settings.LlmBaseUrl) && !string.IsNullOrEmpty(_settings.LlmApiKey))
        {
            try
            {
                var payload = new
                {
                    model = _settings.LlmModel,
                    messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                    temperature = 0.7,
                    max_tokens = 700,
                };
                using var req = new HttpRequestMessage(HttpMethod.Post,
                    _settings.LlmBaseUrl.TrimEnd('/') + "/chat/completions")
                {
                    Content = JsonContent.Create(payload),
                };
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _settings.LlmApiKey);
                using var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var text = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()?
                    .Trim();
                if (!string.IsNullOrEmpty(text))
                    return (text!, "llm");
            }
            catch
            {
                // 任何异常都降级到本地引擎，保证可用
            }
        }
        return ("", "local");
    }

    public async Task<(string Text, string Engine)> ForumReplyAsync(string userContent, bool crisis)
    {
        var (reply, engine) = await ChatAsync(
        [
            new ChatMessage("system", ForumSystemPrompt),
            new ChatMessage("user", userContent),
        ]);
        if (engine == "llm")
        {
            if (crisis && !reply.Contains("12356", StringComparison.Ordinal))
                reply = CrisisPrefix + reply;
            return (reply, engine);
        }
        return (LocalForumFallback(userContent, crisis), "local");
    }

    public async Task<(string Text, string Engine)> AssessAsync(
        string scaleType, int? score, string level, string freeText, bool crisis)
    {
        var scaleName = scaleType switch
        {
            "phq9" => "PHQ-9 抑郁筛查",
            "gad7" => "GAD-7 焦虑筛查",
            "free" => "自由倾诉",
            _ => "心理评估",
        };
        var scoreText = score is null ? "无" : score.ToString();
        var levelText = string.IsNullOrEmpty(level) ? "无" : level;
        var freeTextPart = string.IsNullOrEmpty(freeText) ? "（未填写）" : freeText;
        var userPart =
            $"量表：{scaleName}，得分 {scoreText}，分级：{levelText}。\n" +
            $"用户自述：{freeTextPart}";
        var prompt =
            "你是心光平台的软萌AI陪伴者'小心光'，一个粉发蓝裙的温暖少女。" +
            "说话亲切软萌，像朋友一样，偶尔用可爱语气词（呢、啦、呀），" +
            "但内容保持专业可靠。\n" +
            "请根据用户的心理量表结果和自述，输出：\n" +
            "1) 用通俗的话解释一下这个结果（不做医学确诊）；\n" +
            "2) 你可能现在在经历怎样的情绪呀；\n" +
            "3) 三条小小的、好操作的自助建议；\n" +
            "4) 如果提到了伤害自己的念头，一定要温柔但明确地给出" +
            "12356、010-82951332、110/120 这些求助方式。\n" +
            "字数控制在 400 以内，语气像在跟你好好说话。";

        var (reply, engine) = await ChatAsync(
        [
            new ChatMessage("system", prompt),
            new ChatMessage("user", userPart),
        ]);
        if (engine == "llm")
        {
            if (crisis && !reply.Contains("12356", StringComparison.Ordinal))
                reply = CrisisPrefix + reply;
            return (reply, engine);
        }
        return (LocalAssessFallback(scaleType, score, level, crisis), "local");
    }

    private static string LocalForumFallback(string userContent, bool crisis)
    {
        // 与 Python 相同的 md5 种子，保证同一内容得到稳定的兜底回复（随机序列本身允许不同）
        var md5 = MD5.HashData(Encoding.UTF8.GetBytes(userContent));
        var seed = BitConverter.ToInt32([md5[3], md5[2], md5[1], md5[0]], 0);
        var rng = new Random(seed);

        var parts = new List<string>();
        if (crisis) parts.Add(CrisisPrefix);
        parts.Add(FallbackOpenings[rng.Next(FallbackOpenings.Length)]);
        var tips = SampleTwo(rng);
        parts.Add("可以试着这样照顾自己：\n" + string.Join("\n", tips.Select(t => "· " + t)));
        parts.Add(FallbackEnding);
        return string.Join("\n\n", parts);
    }

    private static string[] SampleTwo(Random rng)
    {
        var i1 = rng.Next(FallbackTips.Length);
        int i2;
        do { i2 = rng.Next(FallbackTips.Length); } while (i2 == i1);
        return [FallbackTips[i1], FallbackTips[i2]];
    }

    private static string LocalAssessFallback(string scaleType, int? score, string level, bool crisis)
    {
        var parts = new List<string>();
        if (crisis) parts.Add(CrisisPrefix);

        if (scaleType is "phq9" or "gad7")
        {
            var scaleName = scaleType == "phq9" ? "PHQ-9 抑郁筛查" : "GAD-7 焦虑筛查";
            parts.Add($"你的{scaleName}得分为 {score} 分，参考分级为「{level}」。");
            parts.Add(
                "量表反映的是近两周情绪状态的倾向，不是医学诊断。分数本身不能定义你，" +
                "但它提示我们：这段时间你的情绪确实承受了较重的负担，值得认真对待。");
        }
        else
        {
            parts.Add("从你的描述里，我读到了持续的压力与情绪消耗。即使暂时说不清原因，这些感受也是真实而重要的。");
        }
        parts.Add(
            "建议你尝试：\n" +
            "· 保持规律作息与基本进食，情绪低谷期身体稳定是恢复的底座；\n" +
            "· 每天安排一次轻度活动（散步10-20分钟）与一次社交连接（给信任的人发条消息）；\n" +
            "· 用呼吸或着陆练习应对急性情绪波峰，并记录情绪与触发事件，便于复诊时和医生沟通。");

        var severe = level is "中重度" or "重度" || crisis;
        if (severe)
            parts.Add(
                "求助指引：当前结果建议你尽快到正规医院心理科/精神科做一次专业评估；" +
                "若已有伤害自己的念头或计划，请立刻拨打 12356 或 010-82951332，紧急情况下拨打 110/120，" +
                "不要独处。");
        else if (level == "中度")
            parts.Add("求助指引：建议在两周内预约学校/社区心理咨询或医院心理科，专业支持能帮你更快恢复。");
        else
            parts.Add("求助指引：如果两周后状态没有改善，或开始影响睡眠、饮食、学习工作，请及时寻求专业咨询。");

        return string.Join("\n\n", parts);
    }
}
