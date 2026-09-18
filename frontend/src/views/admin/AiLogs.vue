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
          <template #default="{ row }"><p class="cell-text">{{ row.response }}</p></template>
        </el-table-column>
        <el-table-column label="时间" min-width="160">
          <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import http from '../../api'
import { fmtTime } from '../../utils/format'

const logs = ref([])
const loading = ref(false)

onMounted(async () => {
  loading.value = true
  try {
    logs.value = await http.get('/admin/ai-logs')
  } finally {
    loading.value = false
  }
})
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
</style>
