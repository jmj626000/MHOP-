/** 后端 SQLite 返回朴素 UTC 时间，补 Z 后按本地时区渲染。 */
function toDate(s) {
  if (!s) return null
  const iso = s.length <= 19 ? s + 'Z' : s
  return new Date(iso)
}

export function fmtTime(s) {
  const d = toDate(s)
  if (!d || isNaN(d)) return ''
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function fromNow(s) {
  const d = toDate(s)
  if (!d || isNaN(d)) return ''
  const diff = Date.now() - d.getTime()
  const min = Math.floor(diff / 60000)
  if (min < 1) return '刚刚'
  if (min < 60) return `${min} 分钟前`
  const hours = Math.floor(min / 60)
  if (hours < 24) return `${hours} 小时前`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days} 天前`
  return fmtTime(s)
}
