<template>
  <div v-loading="loading">
    <el-button text @click="router.back()" style="margin-bottom: 10px; padding-left: 0">
      <el-icon><ArrowLeft /></el-icon> 返回主题列表
    </el-button>

    <template v-if="post">
      <div class="detail-layout">
        <!-- 主楼 + 楼层 -->
        <main class="detail-main">
          <!-- 板块色 Hero -->
          <section class="board-hero" :style="{ background: heroBg }">
            <div class="hero-tags">
              <span class="hero-chip" :style="{ background: '#ffffff2e', color: '#fff' }">
                {{ boardOf(post.board).name }}
              </span>
              <el-tag v-if="post.crisis" size="small" type="danger" effect="dark">危机关注</el-tag>
              <el-tag v-if="post.status === 0" size="small" type="warning" effect="plain">巡检中</el-tag>
            </div>
            <h1>{{ titleText }}</h1>
            <p class="hero-meta">
              {{ post.is_anonymous ? '匿名朋友' : post.author }} · {{ fmtTime(post.created_at) }}
            </p>
          </section>

          <!-- 原帖楼层 -->
          <section ref="topRef" class="floor mhop-card">
            <div class="floor-side">
              <span class="f-avatar"><el-icon><User /></el-icon></span>
              <span class="floor-no">#楼主</span>
            </div>
            <div class="floor-body">
              <div class="floor-head">
                <strong>{{ post.is_anonymous ? '匿名朋友' : post.author }}</strong>
                <span class="text-sub" style="font-size: 12.5px">{{ fmtTime(post.created_at) }}</span>
              </div>
              <el-alert
                v-if="post.crisis"
                type="error"
                :closable="false"
                show-icon
                style="margin: 10px 0"
                title="发布者正处于痛苦中，可能有伤害自己的风险"
                description="如果你也有类似感受，请立即拨打 12356 或 010-82951332；紧急危险请拨打 110/120。回复时请优先传递陪伴与求助信息。"
              />
              <div class="md-body floor-content" v-html="renderMarkdown(post.content)"></div>
              <div class="floor-actions">
                <button class="act-like" :class="{ on: post.liked }" @click="likeTarget('post', post)">
                  <el-icon><component :is="post.liked ? 'StarFilled' : 'Star'" /></el-icon>
                  {{ post.like_count || '送暖心' }}
                </button>
              </div>
            </div>
          </section>

          <!-- 回应楼层 -->
          <section class="replies">
            <h3 ref="replyHeadRef"><el-icon><ChatDotSquare /></el-icon> 温暖回应（{{ post.replies.length }}）</h3>

            <el-alert
              v-if="!aiReady"
              type="success"
              :closable="false"
              show-icon
              style="margin-bottom: 12px"
              title="AI 心理助手正在组织回应，请稍候…"
            />

            <div v-for="(r, idx) in post.replies" :key="r.id"
              :class="['floor', r.is_ai ? 'ai-floor' : 'mhop-card', { recalled: r.recalled }]">
              <div class="floor-side">
                <span class="f-avatar" :class="{ ai: r.is_ai, recalled: r.recalled }">
                  <el-icon><component :is="r.is_ai ? 'MagicStick' : 'User'" /></el-icon>
                </span>
                <span class="floor-no">#{{ idx + 1 }}</span>
              </div>
              <div class="floor-body">
                <!-- 已撤回 AI 回复 -->
                <template v-if="r.recalled">
                  <div class="floor-head">
                    <strong>{{ r.author }}</strong>
                    <el-tag size="small" type="info" effect="dark">已被管理员撤回</el-tag>
                    <el-tag v-if="r.recall_reason" size="small" type="warning" effect="plain">
                      {{ r.recall_reason }}
                    </el-tag>
                  </div>
                  <p class="recalled-text">
                    该 AI 回应因可能存在不当内容已被撤回，不再公开展示。如果你正被痛苦情绪困扰，请拨打 12356 心理援助热线。
                  </p>
                  <div v-if="auth.isAdmin" class="floor-actions">
                    <el-button size="small" type="success" plain @click="restore(r)">
                      <el-icon><RefreshLeft /></el-icon> 恢复显示
                    </el-button>
                  </div>
                </template>

                <template v-else>
                  <div class="floor-head">
                    <strong>{{ r.author }}</strong>
                    <span v-if="r.is_ai" class="ai-badge"><el-icon><MagicStick /></el-icon> AI 即时陪伴</span>
                    <el-tag v-if="r.crisis" size="small" type="danger" effect="light">含安全提示</el-tag>
                    <el-button v-if="auth.isAdmin && r.is_ai" size="small" type="danger" plain
                      class="recall-btn" @click="recall(r)">
                      <el-icon><CircleClose /></el-icon> 撤回
                    </el-button>
                    <span class="text-sub floor-time" style="font-size: 12.5px">{{ fromNow(r.created_at) }}</span>
                  </div>
                  <div class="md-body floor-content" v-html="renderMarkdown(r.content)"></div>
                  <div class="floor-actions">
                    <button class="act-like" :class="{ on: r.liked }" @click="likeTarget('reply', r)">
                      <el-icon><component :is="r.liked ? 'StarFilled' : 'Star'" /></el-icon>
                      {{ r.like_count || '送暖心' }}
                    </button>
                  </div>
                </template>
              </div>
            </div>

            <!-- 本人刚提交、待审核的回复（本地临时展示） -->
            <div v-for="r in myPending" :key="'mine-' + r.id" class="floor mhop-card pending">
              <div class="floor-side">
                <span class="f-avatar"><el-icon><User /></el-icon></span>
              </div>
              <div class="floor-body">
                <div class="floor-head">
                  <strong>我的回复</strong>
                  <el-tag size="small" type="warning" effect="plain">审核通过后公开展示</el-tag>
                </div>
                <div class="md-body floor-content" v-html="renderMarkdown(r.content)"></div>
              </div>
            </div>

            <el-empty v-if="post.replies.length === 0 && aiReady"
              description="还没有人类回应，愿意做第一个温暖 TA 的人吗" :image-size="90" />
          </section>

          <!-- 回复框 -->
          <section class="mhop-card reply-composer">
            <h4>我想回应 TA</h4>
            <p class="text-sub tip">人类回复将先进入系统审核，通过后公开展示；支持 Markdown 排版，请避免评判、说教或提供伤害方法。</p>
            <MdComposer
              v-model="draft"
              :rows="4"
              :maxlength="1000"
              placeholder="也许一句'我在这里陪着你'，就足够有力量……"
            />
            <el-alert
              v-if="replyCrisis"
              type="warning"
              :closable="false"
              show-icon
              style="margin-top: 10px"
              title="检测到危机相关表达，提交后将优先进入人工审核"
            />
            <div class="composer-foot">
              <el-checkbox v-if="auth.isLoggedIn" v-model="replyAnonymous">匿名回复</el-checkbox>
              <el-button type="primary" round :loading="submitting" @click="submit">
                <el-icon><Promotion /></el-icon> 发送回应
              </el-button>
            </div>
          </section>
        </main>

        <!-- 右侧：信息与楼层导航 -->
        <aside class="detail-rail">
          <div class="mhop-card rail-card" ref="bottomRef">
            <div class="rail-stats">
              <span><el-icon><View /></el-icon> {{ post.view_count }} 浏览</span>
              <span><el-icon><ChatLineRound /></el-icon> {{ post.reply_count }} 回应</span>
              <span><el-icon><Star /></el-icon> {{ post.like_count }} 暖心</span>
            </div>
            <el-button class="rail-share" plain @click="share">
              <el-icon><Share /></el-icon> 分享
            </el-button>
            <div class="rail-floors">
              <p class="text-sub">共 {{ post.reply_count }} 条回应</p>
              <el-button text @click="scrollTo('top')"><el-icon><Top /></el-icon> 最早内容</el-button>
              <el-button text @click="scrollTo('reply')"><el-icon><Bottom /></el-icon> 最新回复</el-button>
            </div>
          </div>
        </aside>
      </div>
    </template>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'
import { hasCrisisHint } from '../../utils/crisis'
import { fmtTime, fromNow } from '../../utils/format'
import { boardOf } from '../../utils/boards'
import { renderMarkdown } from '../../utils/markdown'
import MdComposer from '../../components/MdComposer.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const post = ref(null)
const loading = ref(false)
const draft = ref('')
const replyAnonymous = ref(true)
const submitting = ref(false)
const myPending = ref([])
const topRef = ref(null)
const replyHeadRef = ref(null)
let polls = 0
let timer = null

const aiReady = computed(() => post.value?.replies?.some((r) => r.is_ai))
const replyCrisis = computed(() => hasCrisisHint(draft.value))
const titleText = computed(() => {
  const first = (post.value?.content || '').split('\n').find((l) => l.trim()) || ''
  return first.length > 40 ? `${first.slice(0, 40)}…` : first
})
const heroBg = computed(() => {
  const c = boardOf(post.value?.board || 'mood').color
  return `linear-gradient(120deg, ${c} 0%, ${c}cc 55%, ${c}99 100%)`
})

async function fetchPost(incView = false) {
  const data = await http.get(`/forum/posts/${route.params.id}`, { params: incView ? { inc_view: 1 } : {} })
  post.value = data
  if (aiReady.value || polls >= 10) stopPolling()
}

function stopPolling() {
  if (timer) clearInterval(timer)
  timer = null
}

async function likeTarget(type, item) {
  if (!auth.isLoggedIn) {
    ElMessage.warning('登录后才能点赞')
    router.push('/login')
    return
  }
  const data = await http.post('/forum/likes/toggle', { target_type: type, target_id: item.id })
  item.liked = data.liked
  item.like_count = data.like_count
}

async function share() {
  const url = window.location.href
  try {
    await navigator.clipboard.writeText(url)
    ElMessage.success('链接已复制，快去分享给需要的人吧')
  } catch {
    ElMessage.info(url)
  }
}

function scrollTo(where) {
  const el = where === 'top' ? topRef.value : replyHeadRef.value
  if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

async function submit() {
  const content = draft.value.trim()
  if (!content) {
    ElMessage.warning('先写点回应吧')
    return
  }
  submitting.value = true
  try {
    const reply = await http.post(`/forum/posts/${post.value.id}/replies`, {
      content,
      is_anonymous: replyAnonymous.value,
    })
    draft.value = ''
    if (reply.status === 2) {
      ElMessage.error('回复含违规内容，已被系统拦截')
    } else {
      ElMessage.success('回复已提交，审核通过后将公开展示')
      myPending.value.unshift({ id: reply.id, content, created_at: new Date().toISOString() })
      await nextTick()
      scrollTo('reply')
    }
  } finally {
    submitting.value = false
  }
}

async function recall(r) {
  try {
    const { value } = await ElMessageBox.prompt(
      '撤回后该 AI 回复将立即对所有用户隐藏（可恢复）。请填写撤回原因：',
      '撤回 AI 回复',
      {
        confirmButtonText: '确认撤回',
        cancelButtonText: '取消',
        type: 'warning',
        inputPlaceholder: '例如：回复内容不当 / 存在事实错误',
        inputValue: '',
        inputValidator: (v) => (v && v.trim() ? true : '撤回原因必填，便于审计追溯'),
      }
    )
    await http.post(`/admin/replies/${r.id}/recall`, { reason: value.trim() })
    ElMessage.success('AI 回复已撤回')
    await fetchPost()
  } catch (e) {
    /* 用户取消 */
  }
}

async function restore(r) {
  try {
    await ElMessageBox.confirm('恢复后该 AI 回复将重新公开展示，确定吗？', '恢复 AI 回复', {
      confirmButtonText: '恢复显示',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await http.post(`/admin/replies/${r.id}/restore`)
    ElMessage.success('已恢复展示')
    await fetchPost()
  } catch (e) {
    /* 用户取消 */
  }
}

watch(
  () => route.params.id,
  () => {
    stopPolling()
    polls = 0
    myPending.value = []
    load()
  }
)

async function load() {
  loading.value = true
  try {
    await fetchPost(true)
    if (!aiReady.value) {
      timer = setInterval(async () => {
        polls += 1
        await fetchPost()
      }, 3000)
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)
onBeforeUnmount(stopPolling)
</script>

<style scoped>
.detail-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 210px;
  gap: 18px;
  align-items: start;
}
.detail-rail {
  position: sticky;
  top: 12px;
}
.board-hero {
  border-radius: 16px 16px 4px 4px;
  padding: 26px 28px 22px;
  color: #fff;
  margin-bottom: 14px;
}
.hero-tags {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}
.hero-chip {
  font-size: 12.5px;
  font-weight: 600;
  padding: 2px 12px;
  border-radius: 999px;
}
.board-hero h1 {
  margin: 0 0 8px;
  font-size: 23px;
  line-height: 1.45;
  font-weight: 700;
}
.hero-meta {
  margin: 0;
  font-size: 13px;
  opacity: 0.92;
}
.floor {
  padding: 18px 22px;
  margin-bottom: 12px;
  display: flex;
  gap: 16px;
}
.ai-floor {
  background: linear-gradient(180deg, #f0f8f7 0%, #ffffff 100%);
  border: 1px solid #bfe0db;
  border-radius: 14px;
  padding: 18px 22px;
  margin-bottom: 12px;
  display: flex;
  gap: 16px;
}
.floor-side {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  flex: none;
  width: 52px;
}
.f-avatar {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: #e7f3f1;
  color: #2f8f83;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
}
.f-avatar.ai {
  background: linear-gradient(135deg, #2f8f83, #5eaaa1);
  color: #fff;
}
.f-avatar.recalled {
  background: #b6bbc4;
}
.floor-no {
  font-size: 11.5px;
  color: var(--mhop-text-sub);
}
.floor-body {
  flex: 1;
  min-width: 0;
}
.floor-head {
  display: flex;
  align-items: center;
  gap: 9px;
  font-size: 13.5px;
  flex-wrap: wrap;
}
.floor-time {
  margin-left: auto;
}
.floor-content {
  margin-top: 10px;
}
.floor-actions {
  margin-top: 12px;
  display: flex;
  gap: 10px;
}
.act-like {
  border: 1px solid #e2e8e6;
  background: #fff;
  border-radius: 999px;
  padding: 5px 14px;
  font-size: 13px;
  color: var(--mhop-text-sub);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  transition: all 0.15s;
}
.act-like:hover {
  border-color: #e8b46a;
  color: #e0922f;
  background: #fffaf3;
}
.act-like.on {
  border-color: #e8933c;
  color: #e8933c;
  background: #fff7ee;
  font-weight: 600;
}
.recall-btn {
  padding: 4px 10px;
}
.floor.recalled {
  background: #f6f7f9;
  border: 1px dashed #c6cad2;
}
.recalled-text {
  color: #8a8f99;
  font-size: 13.5px;
  margin: 10px 0 0;
}
.floor.pending {
  opacity: 0.78;
  border: 1px dashed #d9c89a;
}
.replies h3 {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 18px 0 12px;
  font-size: 17px;
}
.reply-composer {
  padding: 20px 22px;
  margin-top: 18px;
}
.reply-composer h4 {
  margin: 0 0 4px;
  font-size: 15.5px;
}
.tip {
  font-size: 12.5px;
  margin: 0 0 12px;
}
.composer-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 12px;
}
.rail-card {
  padding: 16px;
}
.rail-stats {
  display: flex;
  flex-direction: column;
  gap: 9px;
  font-size: 13.5px;
  color: var(--mhop-text-sub);
}
.rail-stats span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.rail-share {
  width: 100%;
  margin: 12px 0;
}
.rail-floors {
  border-top: 1px solid #eef1f0;
  padding-top: 10px;
  display: flex;
  flex-direction: column;
  align-items: stretch;
}
.rail-floors p {
  margin: 0 0 6px;
  font-size: 12.5px;
}
.rail-floors .el-button {
  justify-content: flex-start;
  margin: 0;
}
@media (max-width: 860px) {
  .detail-layout {
    grid-template-columns: minmax(0, 1fr);
  }
  .detail-rail {
    position: static;
    min-width: 0;
  }
}
@media (max-width: 640px) {
  .board-hero {
    padding: 18px 16px 16px;
    border-radius: 12px 12px 4px 4px;
  }
  .board-hero h1 {
    font-size: 18px;
  }
  .floor,
  .ai-floor {
    padding: 14px 13px;
    gap: 10px;
  }
  .floor-side {
    width: 40px;
  }
  .f-avatar {
    width: 36px;
    height: 36px;
    font-size: 15px;
  }
  .floor-time {
    margin-left: 0;
  }
  .reply-composer {
    padding: 16px 14px;
  }
  .composer-foot {
    flex-wrap: wrap;
    gap: 10px;
  }
  .rail-stats {
    flex-direction: row;
    flex-wrap: wrap;
    gap: 14px;
  }
  .rail-floors {
    flex-direction: row;
    align-items: center;
    gap: 10px;
  }
  .rail-floors p {
    margin: 0;
  }
}
</style>
