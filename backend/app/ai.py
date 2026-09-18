"""AI 服务适配层。

- 配置了 LLM_BASE_URL + LLM_API_KEY 时，走 OpenAI 兼容 Chat Completions
  （智谱 GLM、DeepSeek、Kimi、OpenAI 均可直接接入）；
- 未配置或调用失败时，降级为内置的共情式规则回复，保证平台在离线/零成本时仍可用。
- 任何引擎下，只要检测到自伤/自杀危机，都强制在回复开头插入援助热线。
"""
import hashlib
import random

import httpx

from .config import settings

CRISIS_PREFIX = (
    "我注意到你正经历非常痛苦、甚至可能想伤害自己的时刻，我很担心你的安全。"
    "请不要独自硬撑：\n"
    "1）立即拨打全国心理援助热线 12356（24小时、免费、保密），"
    "或北京心理危机研究与干预中心 010-82951332；\n"
    "2）如果你觉得自己可能马上做出伤害自己的事，请立刻拨打 110 或 120，"
    "或直接前往最近医院的急诊；\n"
    "3）现在就联系一位你信任的家人或朋友，告诉他/她你的感受，并尽量和人待在一起，远离危险物品。\n\n"
)

FORUM_SYSTEM_PROMPT = (
    "你是全国性公益心理辅助平台的心理支持助手。要求：\n"
    "1. 语气温和、共情、不评判，先接纳情绪，再给出1-3条具体、可执行的适应性调节建议；\n"
    "2. 不做医学诊断，不使用空洞说教，不承诺'一切都会好起来'；\n"
    "3. 回复控制在300字以内；\n"
    "4. 只要用户表达了自伤、自杀、不想活等危机信号，必须在回复最前面用明确文字"
    "给出全国心理援助热线12356、北京心理危机研究与干预中心010-82951332，"
    "并建议立即联系身边人或拨打110/120；\n"
    "5. 不讨论任何伤害自己的具体方法。"
)

_FALLBACK_OPENINGS = [
    "谢谢你愿意把这些说出来，能表达出来本身就需要勇气。",
    "我能感受到你现在的疲惫和不容易，你的感受是真实的，也值得被认真对待。",
    "隔着屏幕，我想先给你一个稳稳的陪伴——你不用一个人扛着这些。",
]

_FALLBACK_TIPS = [
    "先做一个'着陆'练习：慢慢说出你眼前看到的5样东西、听到的4种声音、身体接触到的3样物品，把注意力拉回当下。",
    "尝试4-7-8呼吸：吸气4秒、屏息7秒、缓慢呼气8秒，重复3-4轮，帮助神经系统先安定下来。",
    "把脑子里的想法不加评判地写在纸上，区分'我感受到的情绪'和'我需要解决的问题'，前者需要被看见，后者可以拆成最小的一步。",
    "今天只给自己定一个最小目标（比如按时吃一顿饭、出门走10分钟），完成它就是有效的照顾。",
    "如果情绪在夜晚特别重，可以提前给自己安排一个'情绪急救包'：温热的饮品、能联系的人、一段让你安心的音频。",
    "情绪像海浪，峰值通常会在20-40分钟后回落。在最强烈的时候，先不做任何决定，只让自己安全地度过这一波。",
]

_FALLBACK_ENDING = (
    "\n\n这里是匿名、安全的空间，你可以继续多说一点。"
    "如果这些困扰已经持续两周以上、明显影响睡眠、饮食或日常功能，建议到正规医院心理科/精神科做一次评估——"
    "寻求专业帮助是力量，而不是软弱。"
)


async def chat(messages: list[dict], *, timeout: float = 30.0) -> tuple[str, str]:
    """返回 (回复文本, 引擎标识 llm/local)。"""
    if settings.LLM_BASE_URL and settings.LLM_API_KEY:
        try:
            async with httpx.AsyncClient(timeout=timeout) as client:
                resp = await client.post(
                    settings.LLM_BASE_URL.rstrip("/") + "/chat/completions",
                    headers={"Authorization": f"Bearer {settings.LLM_API_KEY}"},
                    json={
                        "model": settings.LLM_MODEL,
                        "messages": messages,
                        "temperature": 0.7,
                        "max_tokens": 700,
                    },
                )
                resp.raise_for_status()
                data = resp.json()
                return data["choices"][0]["message"]["content"].strip(), "llm"
        except Exception:
            pass  # 任何异常都降级到本地引擎，保证可用
    return "", "local"


async def forum_reply(user_content: str, crisis: bool) -> tuple[str, str]:
    reply, engine = await chat(
        [
            {"role": "system", "content": FORUM_SYSTEM_PROMPT},
            {"role": "user", "content": user_content},
        ]
    )
    if engine == "llm":
        # 双保险：模型漏掉危机提示时本地强制补齐
        if crisis and "12356" not in reply:
            reply = CRISIS_PREFIX + reply
        return reply, engine

    # ---- 本地兜底引擎 ----
    seed = int(hashlib.md5(user_content.encode()).hexdigest()[:8], 16)
    rng = random.Random(seed)
    parts = [CRISIS_PREFIX] if crisis else []
    parts.append(rng.choice(_FALLBACK_OPENINGS))
    tips = rng.sample(_FALLBACK_TIPS, 2)
    parts.append("可以试着这样照顾自己：\n" + "\n".join(f"· {t}" for t in tips))
    parts.append(_FALLBACK_ENDING)
    return "\n\n".join(parts), "local"


async def assess(scale_type: str, score: int | None, level: str, free_text: str, crisis: bool) -> tuple[str, str]:
    scale_name = {"phq9": "PHQ-9 抑郁筛查", "gad7": "GAD-7 焦虑筛查", "free": "自由倾诉"}.get(scale_type, "心理评估")
    user_part = f"量表：{scale_name}，得分 {score if score is not None else '无'}，分级：{level or '无'}。\n用户自述：{free_text or '（未填写）'}"
    prompt = (
        "你是公益心理评估助手。根据用户量表结果与自述，输出：1) 对结果的通俗解释（不做确诊）；"
        "2) 可能的情绪状态分析；3) 三条自助建议；4) 明确的就医/求助指引（出现自伤念头必须给出"
        "12356、010-82951332、110/120）。语言温暖、结构清晰、400字以内。"
    )
    reply, engine = await chat(
        [{"role": "system", "content": prompt}, {"role": "user", "content": user_part}]
    )
    if engine == "llm":
        if crisis and "12356" not in reply:
            reply = CRISIS_PREFIX + reply
        return reply, engine

    parts = [CRISIS_PREFIX] if crisis else []
    if scale_type in ("phq9", "gad7"):
        parts.append(f"你的{scale_name}得分为 {score} 分，参考分级为「{level}」。")
        parts.append(
            "量表反映的是近两周情绪状态的倾向，不是医学诊断。分数本身不能定义你，"
            "但它提示我们：这段时间你的情绪确实承受了较重的负担，值得认真对待。"
        )
    else:
        parts.append("从你的描述里，我读到了持续的压力与情绪消耗。即使暂时说不清原因，这些感受也是真实而重要的。")
    parts.append(
        "建议你尝试：\n"
        "· 保持规律作息与基本进食，情绪低谷期身体稳定是恢复的底座；\n"
        "· 每天安排一次轻度活动（散步10-20分钟）与一次社交连接（给信任的人发条消息）；\n"
        "· 用呼吸或着陆练习应对急性情绪波峰，并记录情绪与触发事件，便于复诊时和医生沟通。"
    )
    severe = level in ("中重度", "重度") or crisis
    if severe:
        parts.append(
            "求助指引：当前结果建议你尽快到正规医院心理科/精神科做一次专业评估；"
            "若已有伤害自己的念头或计划，请立刻拨打 12356 或 010-82951332，紧急情况下拨打 110/120，"
            "不要独处。"
        )
    elif level in ("中度",):
        parts.append(
            "求助指引：建议在两周内预约学校/社区心理咨询或医院心理科，专业支持能帮你更快恢复。"
        )
    else:
        parts.append("求助指引：如果两周后状态没有改善，或开始影响睡眠、饮食、学习工作，请及时寻求专业咨询。")
    return "\n\n".join(parts), "local"
