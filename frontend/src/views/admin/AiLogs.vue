<template>
  <div v-loading="loading">
    <h2 class="page-title">AI 交互日志</h2>
    <el-card shadow="never">
      <el-table :data="logs" stripe>
        <el-table-column label="模块" min-width="110">
          <template #default="{ row }">
            <el-tag :type="row.module === 'forum' ? 'success' : 'warning'" effect="plain" size="small">
              {{ row.module === 'forum' ? '论坛自动回复' : '心理评估' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="引擎" min-width="100">
          <template #default="{ row }">
            <el-tag :type="row.engine === 'llm' ? 'primary' : 'info'" size="small" effect="dark">
              {{ row.engine === 'llm' ? '大模型' : '本地兜底' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="用户输入" min-width="200">
          <template #default="{ row }"><p class="cell-text">{{ row.prompt }}</p></template>
        </el-table-column>
        <el-table-column label="AI 输出" min-width="260">
          <template #default="{ row }">
            <div v-if="row.recalled" class="recalled-line">
              <el-tag type="info" effect="dark" size="small">已撤回</el-tag>
              <el-tag v-if="row.recall_reason" type="warning" effect="plain" size="small" class="reason-tag">
                {{ row.recall_reason }}
              </el-tag>
            </div>
            <p class="cell-text" :class="{ 'is-recalled': row.recalled }">{{ row.response }}</p>
          </template>
        </el-table-column>
        <el-table-column label="时间" min-width="160">
          <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="172">
          <template #default="{ row }">
            <template v-if="canRecall(row)">
              <el-button v-if="!row.recalled" size="small" type="danger" plain
                @click="recall(row)">撤回</el-button>
              <el-button v-else size="small" type="success" plain
                @click="restore(row)">恢复</el-button>
              <el-button size="small" text @click="router.push(`/forum/${row.post_id}`)">查看</el-button>
            </template>
            <span v-else class="op-empty">—</span>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../../api'
import { fmtTime } from '../../utils/format'

const router = useRouter()
const logs = ref([])
const loading = ref(false)

// 仅论坛模块、且日志关联到了实际 AI 回复的记录可以撤回（评估结果为私密内容，不涉及公开展示）
function canRecall(row) {
  return row.module === 'forum' && !!row.reply_id
}

async function loadLogs() {
  loading.value = true
  try {
    logs.value = await http.get('/admin/ai-logs')
  } finally {
    loading.value = false
  }
}

async function recall(row) {
  try {
    const { value } = await ElMessageBox.prompt(
      `撤回该条 AI 回复 #${row.reply_id}：撤回后立即对所有用户隐藏（可恢复）。请填写撤回原因：`,
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
    await http.post(`/admin/replies/${row.reply_id}/recall`, { reason: value.trim() })
    ElMessage.success('AI 回复已撤回')
    loadLogs()
  } catch (e) {
    /* 用户取消 */
  }
}

async function restore(row) {
  try {
    await ElMessageBox.confirm(`恢复 AI 回复 #${row.reply_id} 的公开展示，确定吗？`, '恢复 AI 回复', {
      confirmButtonText: '恢复显示',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await http.post(`/admin/replies/${row.reply_id}/restore`)
    ElMessage.success('已恢复展示')
    loadLogs()
  } catch (e) {
    /* 用户取消 */
  }
}

onMounted(loadLogs)
</script>

<style scoped>
.page-title {
  margin: 0 0 14px;
  font-size: 20px;
}
.cell-text {
  margin: 0;
  font-size: 12.5px;
  line-height: 1.7;
  color: var(--mhop-text);
  display: -webkit-box;
  -webkit-line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
  white-space: pre-wrap;
}
.cell-text.is-recalled {
  color: var(--mhop-text-sub);
}
.recalled-line {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}
.reason-tag {
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.op-empty {
  color: var(--mhop-text-sub);
}
</style>
