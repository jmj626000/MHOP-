/**
 * 轻量、安全的 Markdown 子集渲染器（无第三方依赖）。
 * 先整体 HTML 转义，再做结构化转换，链接仅允许 http/https，杜绝 XSS。
 * 支持：围栏代码块、行内代码、标题、加粗/斜体/删除线、有序/无序列表、引用、分割线、链接、换行。
 */

function escapeHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

function inline(text) {
  // 行内代码
  text = text.replace(/`([^`\n]+)`/g, (_, code) => `<code class="md-code">${code}</code>`)
  // 链接 [文字](http(s)://...)，仅允许 http/https
  text = text.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, (_, label, url) => {
    const safe = url.replace(/"/g, '%22')
    return `<a href="${safe}" target="_blank" rel="nofollow noopener noreferrer">${label}</a>`
  })
  text = text.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
  text = text.replace(/~~([^~]+)~~/g, '<del>$1</del>')
  text = text.replace(/(^|[\s(])\*([^*\n]+)\*/g, '$1<em>$2</em>')
  return text
}

const FENCE_RE = /```[^\n]*\n?([\s\S]*?)```/g

export function renderMarkdown(src) {
  if (!src) return ''
  const blocks = []
  // 1. 抽出围栏代码块
  const escaped = escapeHtml(src).replace(FENCE_RE, (_, code) => {
    blocks.push(code.replace(/\n$/, ''))
    return `\n CODE${blocks.length - 1} \n`
  })
  const lines = escaped.split('\n')
  let html = ''
  let listType = null
  const closeList = () => {
    if (listType) {
      html += `</${listType}>`
      listType = null
    }
  }

  let i = 0
  while (i < lines.length) {
    const raw = lines[i]
    const line = raw

    if (line.trim() === '') {
      closeList()
      i += 1
      continue
    }

    const fence = line.trim().match(/^CODE(\d+)$/)
    if (fence) {
      closeList()
      html += `<pre class="md-pre"><code>${blocks[Number(fence[1])]}</code></pre>`
      i += 1
      continue
    }

    let m = line.match(/^(#{1,4})\s+(.*)$/)
    if (m) {
      closeList()
      const level = m[1].length + 2 // # -> h3
      html += `<h${level} class="md-h">${inline(m[2])}</h${level}>`
      i += 1
      continue
    }

    if (/^\s*(-{3,}|\*{3,})\s*$/.test(line)) {
      closeList()
      html += '<hr class="md-hr"/>'
      i += 1
      continue
    }

    const QUOTE_RE = /^\s*(?:&gt;|>)\s?/
    if (QUOTE_RE.test(line)) {
      closeList()
      const buf = []
      while (i < lines.length && QUOTE_RE.test(lines[i])) {
        buf.push(lines[i].replace(QUOTE_RE, ''))
        i += 1
      }
      html += `<blockquote class="md-quote">${inline(buf.join('<br>'))}</blockquote>`
      continue
    }

    if (/^\s*[-*•]\s+/.test(line)) {
      if (listType !== 'ul') {
        closeList()
        html += '<ul class="md-ul">'
        listType = 'ul'
      }
      html += `<li>${inline(line.replace(/^\s*[-*•]\s+/, ''))}</li>`
      i += 1
      continue
    }

    if (/^\s*\d+[.、)]\s+/.test(line)) {
      if (listType !== 'ol') {
        closeList()
        html += '<ol class="md-ol">'
        listType = 'ol'
      }
      html += `<li>${inline(line.replace(/^\s*\d+[.、)]\s+/, ''))}</li>`
      i += 1
      continue
    }

    // 普通段落，合并连续文本行
    closeList()
    const buf = [line]
    i += 1
    const stop = (l) =>
      l.trim() === '' ||
      /^(#{1,4})\s/.test(l) ||
      /^\s*[-*•]\s/.test(l) ||
      /^\s*\d+[.、)]\s/.test(l) ||
      /^\s*(?:&gt;|>)/.test(l) ||
      /^\s*(-{3,}|\*{3,})\s*$/.test(l) ||
      /^CODE\d+$/.test(l.trim())
    while (i < lines.length && !stop(lines[i])) {
      buf.push(lines[i])
      i += 1
    }
    html += `<p class="md-p">${inline(buf.join('<br>'))}</p>`
  }
  closeList()
  return html
}
