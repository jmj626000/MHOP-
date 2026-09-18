/** 板块元数据（与后端 app/boards.py 保持一致），用于标签即时着色；数量以 /forum/boards 接口为准。 */
export const BOARDS = [
  { slug: 'crisis', name: '危机求助', color: '#e0524d', desc: '出现自伤/自杀念头或紧急风险，请优先拨打援助热线' },
  { slug: 'mood', name: '情绪树洞', color: '#2f8f83', desc: '抑郁、焦虑、低落、崩溃，想找个安全的地方说说' },
  { slug: 'stress', name: '压力倾诉', color: '#e8933c', desc: '学业、工作、家庭与生活压力' },
  { slug: 'relation', name: '人际情感', color: '#d6668f', desc: '亲情、友情、爱情等人际关系困扰' },
  { slug: 'sleep', name: '睡眠困扰', color: '#6a6fd0', desc: '失眠、多梦、作息紊乱与精神疲惫' },
  { slug: 'recovery', name: '康复经验', color: '#4e9e5f', desc: '好转经历与自我调节方法分享' },
  { slug: 'chat', name: '随便聊聊', color: '#8a8f99', desc: '不属于以上板块，只想找人说说话' },
]

export const BOARD_MAP = Object.fromEntries(BOARDS.map((b) => [b.slug, b]))

export function boardOf(slug) {
  return BOARD_MAP[slug] || BOARDS[1]
}

/** 加深颜色（用于标签文字/边框），输入 #rrggbb */
export function shade(hex, percent) {
  const n = parseInt(hex.slice(1), 16)
  const amt = Math.round(2.55 * percent)
  const r = Math.max(0, Math.min(255, (n >> 16) + amt))
  const g = Math.max(0, Math.min(255, ((n >> 8) & 0xff) + amt))
  const b = Math.max(0, Math.min(255, (n & 0xff) + amt))
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`
}
