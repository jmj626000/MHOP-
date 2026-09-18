"""标准心理量表：PHQ-9（抑郁）与 GAD-7（焦虑）。"""

OPTION_SET = [
    {"label": "完全不会", "value": 0},
    {"label": "有几天", "value": 1},
    {"label": "一半以上的天数", "value": 2},
    {"label": "几乎每天", "value": 3},
]

SCALES = {
    "phq9": {
        "key": "phq9",
        "name": "PHQ-9 抑郁症筛查量表",
        "intro": "根据过去两周的状况作答。PHQ-9 是国际通用的抑郁症状筛查工具，结果仅供自我了解，不构成诊断。",
        "max": 27,
        "options": OPTION_SET,
        "questions": [
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
        "bands": [
            (4, "无明显症状", "normal"),
            (9, "轻度", "mild"),
            (14, "中度", "moderate"),
            (19, "中重度", "severe"),
            (27, "重度", "danger"),
        ],
        "crisis_index": 8,  # 第9题 > 0 即危机信号
    },
    "gad7": {
        "key": "gad7",
        "name": "GAD-7 广泛性焦虑筛查量表",
        "intro": "根据过去两周的状况作答。GAD-7 是国际通用的焦虑症状筛查工具，结果仅供自我了解，不构成诊断。",
        "max": 21,
        "options": OPTION_SET,
        "questions": [
            "感到紧张、焦虑或急躁",
            "不能停止或控制担忧",
            "对各种各样的事情担忧过多",
            "很难放松下来",
            "由于不安而无法静坐",
            "变得容易烦恼或急躁",
            "感到似乎将有可怕的事情发生而害怕",
        ],
        "bands": [
            (4, "无明显症状", "normal"),
            (9, "轻度", "mild"),
            (14, "中度", "moderate"),
            (21, "重度", "danger"),
        ],
        "crisis_index": None,
    },
}


def score_band(scale_key: str, score: int) -> tuple[str, str]:
    bands = SCALES[scale_key]["bands"]
    for ceiling, label, code in bands:
        if score <= ceiling:
            return label, code
    return bands[-1][1], bands[-1][2]
