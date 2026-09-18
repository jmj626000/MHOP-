/** 前端侧轻量危机词预判，用于即时弹出援助提示；以后端判定为准。 */
const CRISIS_HINTS = [
  '自杀', '轻生', '不想活', '活着没意思', '活不下去', '活够了', '结束生命',
  '自残', '割腕', '跳楼', '上吊', '烧炭', '伤害自己', '撑不下去', '解脱',
]

export function hasCrisisHint(text = '') {
  return CRISIS_HINTS.some((w) => text.includes(w))
}
