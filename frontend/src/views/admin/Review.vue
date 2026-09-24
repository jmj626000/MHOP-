<template>
  <div v-loading="loading">
    <h2 class="page-title">内容审核</h2>

    <el-tabs v-model="tab" @tab-change="reload">
      <!-- 帖子 -->
      <el-tab-pane name="posts">
        <template #label>
          <el-badge :value="pendingCount.posts" :hidden="pendingCount.posts === 0" type="danger">
            <span style="padding: 0 10px">帖子巡检</span>
          </el-badge>
        </template>

        <el-radio-group v-model="postStatus" size="small" style="margin-bottom: 12px" @change="reload">
          <el-radio-button :value="0">待巡检</el-radio-button>
          <el-radio-button :value="1">正常</el-radio-button>
          <el-radio-button :value="2">已隐藏</el-radio-button>
        </el-radio-group>

        <el-table :data="posts" stripe>
          <el-table-column label="内容" min-width="320">
            <template #default="{ row }">
              <p class="cell-content">{{ row.content }}</p>
              <div style="margin-top: 6px">
                <span class="board-chip" :style="chipStyle(row.board)">{{ boardOf(row.board).name }}</span>
                <el-tag v-if="row.crisis" size="small" type="danger" effect="light" style="margin-left: 4px">危机信号</el-tag>
                <el-tag size="small" :type="row.is_anonymous ? 'warning' : 'success'" effect="plain" style="margin-left: 4px">
                  {{ authorTag(row) }}
                </el-tag>
                <el-tag v-if="row.review_note" size="small" type="info" effect="plain" style="margin-left: 4px">
                  备注：{{ row.review_note }}
                </el-tag>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="状态" width="100">
            <template #default="{ row }">
              <el-tag size="small" :type="postStatusType(row.status)" effect="dark">
                {{ ['待巡检', '正常', '已隐藏'][row.status] }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="回应" width="70" prop="reply_count" />
          <el-table-column label="发布时间" width="165">
            <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="190" fixed="right">
            <template #default="{ row }">
              <el-button v-if="row.status !== 1" size="small" type="success" @click="moderate('post', row, 'approve')">
                标记正常
              </el-button>
              <el-button v-if="row.status !== 2" size="small" type="danger" @click="moderate('post', row, 'reject')">
                隐藏
              </el-button>
              <el-button size="small" text @click="$router.push(`/forum/${row.id}`)">查看</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <!-- 回复 -->
      <el-tab-pane name="replies">
        <template #label>
          <el-badge :value="pendingCount.replies" :hidden="pendingCount.replies === 0" type="danger">
            <span style="padding: 0 10px">回复审核</span>
          </el-badge>
        </template>

        <el-radio-group v-model="replyStatus" size="small" style="margin-bottom: 12px" @change="reload">
          <el-radio-button :value="0">待审核</el-radio-button>
          <el-radio-button :value="1">已通过</el-radio-button>
          <el-radio-button :value="2">已驳回/拦截</el-radio-button>
        </el-radio-group>

        <el-table :data="replies" stripe>
          <el-table-column label="所属帖子" width="200">
            <template #default="{ row }">
              <p class="cell-excerpt">{{ row.post_excerpt }}</p>
              <el-button link type="primary" size="small" @click="$router.push(`/forum/${row.post_id}`)">
                打开原帖 #{{ row.post_id }}
              </el-button>
            </template>
          </el-table-column>
          <el-table-column label="回复内容" min-width="300">
            <template #default="{ row }">
              <p class="cell-content">{{ row.content }}</p>
              <div style="margin-top: 6px">
                <el-tag v-if="row.is_ai" size="small" type="success">AI 回复</el-tag>
                <el-tag v-else size="small" :type="row.is_anonymous ? 'warning' : 'success'" effect="plain" style="margin-left: 4px">
                  {{ authorTag(row) }}
                </el-tag>
                <el-tag v-if="row.recalled" size="small" type="info" effect="dark" style="margin-left: 4px">已撤回</el-tag>
                <el-tag v-if="row.crisis" size="small" type="danger" effect="light" style="margin-left: 4px">危机信号</el-tag>
                <el-tag v-if="row.recall_reason" size="small" type="warning" effect="plain" style="margin-left: 4px">
                  撤回原因：{{ row.recall_reason }}
                </el-tag>
                <el-tag v-else-if="row.review_note" size="small" type="info" effect="plain" style="margin-left: 4px">
                  {{ row.review_note }}
                </el-tag>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="状态" width="100">
            <template #default="{ row }">
              <el-tag size="small" :type="replyStatusType(row.status)" effect="dark">
                {{ ['待审核', '已通过', '已驳回'][row.status] }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="时间" width="165">
            <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="210" fixed="right">
            <template #default="{ row }">
              <template v-if="!row.is_ai">
                <el-button v-if="row.status !== 1" size="small" type="success"
                  @click="moderate('reply', row, 'approve')">通过</el-button>
                <el-button v-if="row.status !== 2" size="small" type="danger"
                  @click="moderate('reply', row, 'reject')">驳回</el-button>
              </template>
              <template v-else>
                <el-button v-if="!row.recalled" size="small" type="danger" plain
                  @click="recallReply(row)">撤回</el-button>
                <el-button v-else size="small" type="success" plain
                  @click="restoreReply(row)">恢复</el-button>
                <el-button size="small" text @click="$router.push(`/forum/${row.post_id}`)">查看</el-button>
              </template>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../../api'
import { fmtTime } from '../../utils/format'
import { boardOf } from '../../utils/boards'

const tab = ref('posts')
const postStatus = ref(0)
const replyStatus = ref(0)
const posts = ref([])
const replies = ref([])
const loading = ref(false)
const pendingCount = reactive({ posts: 0, replies: 0 })

function authorTag(row) {
  if (row.is_ai) return 'AI 回复'
  if (!row.is_anonymous) return row.author || '实名用户'
  // 匿名帖对前台匿名，后台展示真实作者与绑定手机号
  if (row.author) {
    return `匿名·实名：${row.author}${row.author_phone ? ' / ' + row.author_phone : ''}`
  }
  return '匿名（历史记录不可追溯）'
}
function postStatusType(s) {
  return ['warning', 'success', 'danger'][s] || 'info'
}
function replyStatusType(s) {
  return ['warning', 'success', 'danger'][s] || 'info'
}
function chipStyle(slug) {
  const b = boardOf(slug || 'mood')
  return {
    background: `${b.color}1a`,
    color: b.color,
    border: `1px solid ${b.color}55`,
  }
}

async function loadPending() {
  const s = await http.get('/admin/stats')
  pendingCount.posts = s.pending_posts
  pendingCount.replies = s.pending_replies
}

async function reload() {
  loading.value = true
  try {
    if (tab.value === 'posts') {
      posts.value = await http.get('/admin/posts', { params: { status: postStatus.value } })
    } else {
      replies.value = await http.get('/admin/replies', { params: { status: replyStatus.value } })
    }
    loadPending()
  } finally {
    loading.value = false
  }
}

async function moderate(kind, row, action) {
  const isReject = action === 'reject'
  const label = kind === 'post' ? (isReject ? '隐藏该帖（前端将不再展示）' : '标记为正常') : isReject ? '驳回该回复' : '通过该回复'
  try {
    const { value } = await ElMessageBox.prompt(`${label}，可填写审核备注（可选）`, '审核确认', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      inputPlaceholder: '例如：含自伤风险，已转危机干预',
      inputValue: '',
    })
    await http.post(`/admin/${kind === 'post' ? 'posts' : 'replies'}/${row.id}/moderate`, {
      action,
      note: value || '',
    })
    ElMessage.success('已处理')
    reload()
  } catch (e) {
    /* 用户取消 */
  }
}

async function recallReply(row) {
  try {
    const { value } = await ElMessageBox.prompt(
      `撤回 AI 回复 #${row.id}：撤回后立即对所有用户隐藏（可恢复）。请填写撤回原因：`,
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
    await http.post(`/admin/replies/${row.id}/recall`, { reason: value.trim() })
    ElMessage.success('AI 回复已撤回')
    reload()
  } catch (e) {
    /* 用户取消 */
  }
}

async function restoreReply(row) {
  try {
    await ElMessageBox.confirm(`恢复 AI 回复 #${row.id} 的公开展示，确定吗？`, '恢复 AI 回复', {
      confirmButtonText: '恢复显示',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await http.post(`/admin/replies/${row.id}/restore`)
    ElMessage.success('已恢复展示')
    reload()
  } catch (e) {
    /* 用户取消 */
  }
}

onMounted(reload)
</script>

<style scoped>
.page-title {
  margin: 0 0 14px;
  font-size: 20px;
}
.cell-content {
  margin: 0;
  white-space: pre-wrap;
  line-height: 1.7;
  font-size: 13.5px;
}
.cell-excerpt {
  margin: 0 0 4px;
  font-size: 12.5px;
  color: var(--mhop-text-sub);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.board-chip {
  font-size: 12px;
  padding: 1px 9px;
  border-radius: 999px;
  font-weight: 600;
}
</style>
