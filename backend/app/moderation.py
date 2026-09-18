"""内容安全：危机词识别（触发强制援助提示）+ 基础敏感词巡检。

生产环境可在此替换为云厂商内容安全 API（百度/阿里云内容审核），
调用方仅依赖 detect_crisis / hit_sensitive 两个函数，无需改动路由。
"""

# 自伤 / 自杀危机信号
CRISIS_WORDS = [
    "自杀", "自尽", "轻生", "不想活", "活着没意思", "活不下去", "活够了",
    "结束生命", "离开这个世界", "一了百了", "找死", "寻死", "同归于尽",
    "自残", "割腕", "割自己", "跳楼", "上吊", "烧炭", "安眠药", "伤害自己",
    "毁掉自己", "撑不下去了", "解脱",
]

# 基础违规词（演示词库；正式上线请替换为专业敏感词库 / 云审核 API）
SENSITIVE_WORDS = [
    "色情", "裸体", "约炮", "迷奸", "冰毒", "海洛因", "卖毒品", "枪支",
    "炸药", "赌博网站", "刷单", "代开发票", "私家侦探", "高利贷",
    "加微信领", "点击链接", "六合彩", "代考",
]


def detect_crisis(text: str) -> bool:
    return any(w in text for w in CRISIS_WORDS)


def hit_sensitive(text: str) -> list[str]:
    return [w for w in SENSITIVE_WORDS if w in text]
