<template>
  <div class="forum-layout">
    <!-- 左侧：统计 / 发帖 / 板块导航 -->
    <aside class="forum-side">
      <div class="mhop-card side-card">
        <h4><el-icon><DataAnalysis /></el-icon> 论坛统计</h4>
        <div class="stat-grid">
          <div><strong>{{ fmtK(stats.posts) }}</strong><span>主题</span></div>
          <div><strong>{{ fmtK(stats.replies) }}</strong><span>回复</span></div>
          <div><strong>{{ fmtK(stats.users) }}</strong><span>用户</span></div>
        </div>
        <p class="side-online text-sub">
          <span class="online-dot" /> {{ stats.online }} 人正在线
        </p>
      </div>

      <el-button class="new-topic-btn" type="primary" size="large" @click="openComposer">
        <el-icon><EditPen /></el-icon> 发布主题
      </el-button>

      <nav class="mhop-card board-nav">
        <a :class="{ active: board === '' }" @click="selectBoard('')">
          <span class="dot" style="background: #2f8f83" /> 全部主题
          <span class="cnt">{{ stats.posts }}</span>
        </a>
        <a v-for="b in boards" :key="b.slug" :class="{ active: board === b.slug }" @click="selectBoard(b.slug)">
          <span class="dot" :style="{ background: b.color }" /> {{ b.name }}
          <span class="cnt">{{ b.count }}</span>
        </a>
      </nav>
    </aside>

    <!-- 中间：主题流 -->
    <main class="forum-main">
      <div class="stream-head">
        <div class="stream-title">
          <h2>{{ board ? boardOf(board).name : '全部主题' }}</h2>
          <el-select v-model="sort" size="small" style="width: 118px" @change="() => reload(1)">
            <el-option label="最新回复" value="latest" />
            <el-option label="最新发布" value="new" />
          </el-select>
        </div>
        <el-input
          v-model="keyword"
          placeholder="搜索帖子内容"
          clearable
          style="width: 240px"
          :prefix-icon="Search"
          @keyup.enter="reload(1)"
          @clear="reload(1)"
        />
      </div>

      <div class="topic-list mhop-card" v-loading="loading">
        <div v-for="p in posts" :key="p.id" class="topic-row" @click="router.push(`/forum/${p.id}`)">
          <span class="t-avatar"><el-icon><User /></el-icon></span>
          <div class="t-main">
            <p class="t-title">{{ firstLine(p.content) }}</p>
            <p class="t-excerpt">{{ p.content }}</p>
            <div class="t-tags">
              <span class="board-chip" :style="chipStyle(p.board)">{{ boardOf(p.board).name }}</span>
              <el-tag v-if="p.crisis" size="small" type="danger" effect="light">危机</el-tag>
              <el-tag v-if="p.ai_replied" size="small" type="success" effect="light">
                <el-icon><MagicStick /></el-icon> AI 已回应
              </el-tag>
              <el-tag v-if="p.status === 0" size="small" type="warning" effect="plain">巡检中</el-tag>
              <span class="t-author">{{ p.author }} · 发布于 {{ fromNow(p.created_at) }}</span>
              <span v-if="p.last_reply_at" class="t-last">
                <el-icon><Back /></el-icon>{{ p.last_reply_author }} · {{ fromNow(p.last_reply_at) }}
              </span>
            </div>
          </div>
          <div class="t-stats" @click.stop>
            <span class="stat"><el-icon><ChatLineRound /></el-icon> {{ p.reply_count }}</span>
            <span class="stat"><el-icon><View /></el-icon> {{ fmtK(p.view_count) }}</span>
            <button class="like-btn" :class="{ on: p.liked }" @click="togglePostLike(p)">
              <el-icon><component :is="p.liked ? 'StarFilled' : 'Star'" /></el-icon>
              {{ p.like_count || '暖心' }}
            </button>
          </div>
        </div>
        <el-empty v-if="!loading && posts.length === 0" :description="board ? '该板块还没有帖子' : '还没有匹配的帖子'" />
      </div>

      <div v-if="posts.length < total && posts.length" class="load-more">
        <el-button round plain :loading="loadingMore" @click="loadMore">
          {{ loadingMore ? '加载中…' : '加载更多' }}
        </el-button>
      </div>
    </main>

    <!-- 发帖弹窗 -->
    <el-dialog v-model="composerVisible" title="发布主题" width="640px" @closed="resetForm">
      <div class="composer-boards">
        <span class="cb-label">选择板块</span>
        <el-radio-group v-model="form.board" size="small">
          <el-radio-button v-for="b in boards" :key="b.slug" :value="b.slug">{{ b.name }}</el-radio-button>
        </el-radio-group>
      </div>
      <p class="board-desc text-sub">
        <span class="desc-dot" :style="{ background: boardOf(form.board).color }" />
        {{ boardOf(form.board).desc }}
      </p>
      <MdComposer
        v-model="form.content"
        :rows="5"
        :maxlength="2000"
        placeholder="我在这里，你可以放心说。发布后 AI 心理助手会立刻回应你……"
      />
      <el-alert
        v-if="crisis"
        class="crisis-alert"
        type="error"
        :closable="false"
        show-icon
        title="你的文字里出现了伤害自己的信号，我们非常担心你"
        description="请立即拨打全国心理援助热线 12356（24小时）或 010-82951332；若有立即危险请拨打 110/120。你依然可以发布，AI 会优先给你安全回应。"
      />
      <template #footer>
        <div class="composer-foot">
          <el-checkbox v-if="auth.isLoggedIn" v-model="form.is_anonymous">匿名发布</el-checkbox>
          <span v-else class="text-sub" style="font-size: 12.5px">匿名访问，无需注册</span>
          <el-button type="primary" round :loading="submitting" @click="submit">
            <el-icon><Promotion /></el-icon> 发布并获得 AI 回应
          </el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'
import { hasCrisisHint } from '../../utils/crisis'
import { fromNow } from '../../utils/format'
import { BOARDS, boardOf } from '../../utils/boards'
import MdComposer from '../../components/MdComposer.vue'

const router = useRouter()
const auth = useAuthStore()

const boards = ref(BOARDS.map((b) => ({ ...b, count: 0 })))
const stats = ref({ posts: 0, replies: 0, users: 0, online: 0 })
const posts = ref([])
const total = ref(0)
const page = ref(1)
const size = 12
const loading = ref(false)
const loadingMore = ref(false)
const keyword = ref('')
const board = ref('')
const sort = ref('latest')

const composerVisible = ref(false)
const form = ref({ board: 'mood', content: '', is_anonymous: true })
const submitting = ref(false)
const crisis = computed(() => hasCrisisHint(form.value.content))

function firstLine(content) {
  return (content || '').split('\n').find((l) => l.trim()) || '（无内容）'
}
function fmtK(n) {
  if (n == null) return 0
  return n >= 1000 ? `${(n / 1000).toFixed(1)}千` : n
}
function chipStyle(slug) {
  const b = boardOf(slug)
  return {
    background: `${b.color}1a`,
    color: b.color,
    border: `1px solid ${b.color}55`,
  }
}

async function fetchSide() {
  const [b, s] = await Promise.all([http.get('/forum/boards'), http.get('/forum/stats')])
  boards.value = b
  stats.value = s
}

async function reload(p = 1) {
  loading.value = true
  page.value = p
  try {
    const data = await http.get('/forum/posts', {
      params: { page: p, size, keyword: keyword.value, board: board.value, sort: sort.value },
    })
    posts.value = data.items
    total.value = data.total
  } finally {
    loading.value = false
  }
}

async function loadMore() {
  loadingMore.value = true
  try {
    const data = await http.get('/forum/posts', {
      params: { page: page.value + 1, size, keyword: keyword.value, board: board.value, sort: sort.value },
    })
    posts.value.push(...data.items)
    page.value = data.page
  } finally {
    loadingMore.value = false
  }
}

function selectBoard(slug) {
  board.value = slug
  reload(1)
}

async function togglePostLike(p) {
  if (!auth.isLoggedIn) {
    ElMessage.warning('登录后才能点赞，匿名浏览不记录身份哦')
    router.push('/login')
    return
  }
  const data = await http.post('/forum/likes/toggle', { target_type: 'post', target_id: p.id })
  p.liked = data.liked
  p.like_count = data.like_count
}

function openComposer() {
  form.value = { board: board.value || 'mood', content: '', is_anonymous: true }
  composerVisible.value = true
}
function resetForm() {
  form.value = { board: 'mood', content: '', is_anonymous: true }
}

async function submit() {
  const content = form.value.content.trim()
  if (!content) {
    ElMessage.warning('先写点什么吧')
    return
  }
  submitting.value = true
  try {
    const post = await http.post('/forum/posts', {
      content,
      is_anonymous: form.value.is_anonymous,
      board: form.value.board,
    })
    ElMessage.success('已发布，AI 心理助手正在回应…')
    composerVisible.value = false
    await fetchSide()
    if (board.value && board.value !== form.value.board) {
      board.value = ''
    }
    await reload(1)
    router.push(`/forum/${post.id}`)
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  fetchSide()
  reload(1)
})
</script>

<style scoped>
.stream-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  gap: 12px;
  flex-wrap: wrap;
}
.stream-title {
  display: flex;
  align-items: center;
  gap: 14px;
}
.stream-title h2 {
  margin: 0;
  font-size: 20px;
}
.topic-list {
  padding: 4px 8px;
}
.topic-row {
  display: flex;
  gap: 13px;
  padding: 16px 14px;
  border-radius: 12px;
  cursor: pointer;
  border-bottom: 1px solid #f0f2f1;
  transition: background 0.15s;
}
.topic-row:last-child {
  border-bottom: none;
}
.topic-row:hover {
  background: #f7fbfa;
}
.t-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: #e7f3f1;
  color: #2f8f83;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  flex: none;
}
.t-main {
  flex: 1;
  min-width: 0;
}
.t-title {
  margin: 0;
  font-size: 15.5px;
  font-weight: 700;
  color: var(--mhop-text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.t-excerpt {
  margin: 4px 0 7px;
  font-size: 13.5px;
  color: var(--mhop-text-sub);
  line-height: 1.6;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.t-tags {
  display: flex;
  align-items: center;
  gap: 7px;
  flex-wrap: wrap;
  font-size: 12.5px;
  color: var(--mhop-text-sub);
}
.board-chip {
  font-size: 12px;
  padding: 1px 9px;
  border-radius: 999px;
  font-weight: 600;
}
.t-author {
  margin-left: 2px;
}
.t-last {
  display: inline-flex;
  align-items: center;
  gap: 3px;
}
.t-stats {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  justify-content: center;
  gap: 7px;
  flex: none;
  min-width: 74px;
}
.t-stats .stat {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  color: var(--mhop-text-sub);
}
.like-btn {
  border: none;
  background: none;
  cursor: pointer;
  color: var(--mhop-text-sub);
  font-size: 12.5px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 6px;
  border-radius: 999px;
  transition: all 0.15s;
}
.like-btn:hover {
  background: #fff5e8;
  color: #e0922f;
}
.like-btn.on {
  color: #e8933c;
  font-weight: 600;
}
.load-more {
  text-align: center;
  margin: 16px 0 6px;
}
.online-dot {
  display: inline-block;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #4e9e5f;
  margin-right: 4px;
}
.composer-boards {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 8px;
}
.cb-label {
  font-weight: 600;
  font-size: 14px;
}
.board-desc {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 12.5px;
  margin: 0 0 12px;
}
.desc-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.crisis-alert {
  margin-top: 12px;
}
.composer-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
