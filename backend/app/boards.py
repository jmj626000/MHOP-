"""论坛板块（心理场景适配）。slug 入库，颜色仅用于前端展示。"""
from typing import TypedDict


class Board(TypedDict):
    slug: str
    name: str
    color: str
    desc: str


BOARDS: list[Board] = [
    {"slug": "crisis", "name": "危机求助", "color": "#e0524d",
     "desc": "出现自伤/自杀念头或紧急风险，请优先拨打援助热线"},
    {"slug": "mood", "name": "情绪树洞", "color": "#2f8f83",
     "desc": "抑郁、焦虑、低落、崩溃，想找个安全的地方说说"},
    {"slug": "stress", "name": "压力倾诉", "color": "#e8933c",
     "desc": "学业、工作、家庭与生活压力"},
    {"slug": "relation", "name": "人际情感", "color": "#d6668f",
     "desc": "亲情、友情、爱情等人际关系困扰"},
    {"slug": "sleep", "name": "睡眠困扰", "color": "#6a6fd0",
     "desc": "失眠、多梦、作息紊乱与精神疲惫"},
    {"slug": "recovery", "name": "康复经验", "color": "#4e9e5f",
     "desc": "好转经历与自我调节方法分享"},
    {"slug": "chat", "name": "随便聊聊", "color": "#8a8f99",
     "desc": "不属于以上板块，只想找人说说话"},
]

BOARD_SLUGS = {b["slug"] for b in BOARDS}
BOARD_NAMES = {b["slug"]: b["name"] for b in BOARDS}
