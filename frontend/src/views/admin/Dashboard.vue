<template>
  <div v-loading="loading">
    <h2 class="page-title">数据看板</h2>

    <el-row :gutter="16">
      <el-col v-for="c in cards" :key="c.label" :xs="12" :sm="8" :md="6" :lg="6" :xl="4">
        <div class="stat-card" :style="{ background: c.bg }">
          <el-icon :size="26"><component :is="c.icon" /></el-icon>
          <div class="stat-num">{{ c.value }}</div>
          <div class="stat-label">{{ c.label }}</div>
        </div>
      </el-col>
    </el-row>

    <el-row :gutter="16" style="margin-top: 18px">
      <el-col :xs="24" :md="12">
        <el-card shadow="never" class="panel">
          <template #header><strong>待办与活跃</strong></template>
          <ul class="todo-list">
            <li @click="$router.push('/admin/review')">
              <el-tag type="warning" effect="light">待巡检帖子</el-tag>
              <span class="num">{{ stats.pending_posts ?? 0 }}</span>
              <el-icon class="arrow"><ArrowRight /></el-icon>
            </li>
            <li @click="$router.push('/admin/review')">
              <el-tag type="warning" effect="light">待审核人类回复</el-tag>
              <span class="num">{{ stats.pending_replies ?? 0 }}</span>
              <el-icon class="arrow"><ArrowRight /></el-icon>
            </li>
            <li @click="$router.push('/admin/review')">
              <el-tag type="danger" effect="light">在线危机帖（未隐藏）</el-tag>
              <span class="num danger">{{ stats.crisis_posts ?? 0 }}</span>
              <el-icon class="arrow"><ArrowRight /></el-icon>
            </li>
            <li @click="$router.push('/admin/review')">
              <el-tag type="info" effect="light">系统拦截回复</el-tag>
              <span class="num">{{ stats.rejected_replies ?? 0 }}</span>
              <el-icon class="arrow"><ArrowRight /></el-icon>
            </li>
          </ul>
        </el-card>
      </el-col>
      <el-col :xs="24" :md="12">
        <el-card shadow="never" class="panel">
          <template #header><strong>近 24 小时趋势</strong></template>
          <ul class="todo-list">
            <li>
              <el-tag type="success" effect="light">当前在线访客</el-tag>
              <span class="num">{{ stats.online ?? 0 }}</span>
            </li>
            <li>
              <el-tag type="success" effect="light">新增帖子</el-tag>
              <span class="num">{{ stats.new_posts_24h ?? 0 }}</span>
            </li>
            <li>
              <el-tag type="success" effect="light">新增注册</el-tag>
              <span class="num">{{ stats.new_users_24h ?? 0 }}</span>
            </li>
            <li>
              <el-tag type="success" effect="light">AI 调用累计</el-tag>
              <span class="num">{{ stats.ai_logs ?? 0 }}</span>
            </li>
          </ul>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="never" style="margin-top: 18px">
      <template #header><strong>累计数据</strong></template>
      <el-descriptions :column="4" border>
        <el-descriptions-item label="注册用户">{{ stats.users ?? 0 }}</el-descriptions-item>
        <el-descriptions-item label="帖子总量">{{ stats.posts ?? 0 }}</el-descriptions-item>
        <el-descriptions-item label="回复总量">{{ stats.replies ?? 0 }}</el-descriptions-item>
        <el-descriptions-item label="云端测评记录">{{ stats.assessments ?? 0 }}</el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import http from '../../api'

const stats = ref({})
const loading = ref(false)
let timer = null

const cards = computed(() => [
  { label: '注册用户', value: stats.value.users, icon: 'UserFilled', bg: '#e7f3f1' },
  { label: '论坛帖子', value: stats.value.posts, icon: 'ChatLineSquare', bg: '#eef4fb' },
  { label: '回复总数', value: stats.value.replies, icon: 'ChatDotRound', bg: '#f3eefa' },
  { label: 'AI 测评记录', value: stats.value.assessments, icon: 'DocumentChecked', bg: '#fdf3e7' },
  { label: '待处理审核', value: (stats.value.pending_posts || 0) + (stats.value.pending_replies || 0), icon: 'Bell', bg: '#fcf0e6' },
  { label: '在线访客', value: stats.value.online, icon: 'Connection', bg: '#e9f7ee' },
])

async function load() {
  loading.value = true
  try {
    stats.value = await http.get('/admin/stats')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  load()
  timer = setInterval(load, 30000)
})
onBeforeUnmount(() => timer && clearInterval(timer))
</script>

<style scoped>
.page-title {
  margin: 0 0 18px;
  font-size: 20px;
}
.stat-card {
  border-radius: 14px;
  padding: 18px;
  color: #227067;
  margin-bottom: 16px;
}
.stat-num {
  font-size: 30px;
  font-weight: 800;
  margin: 6px 0 2px;
  color: var(--mhop-text);
}
.stat-label {
  font-size: 13px;
  color: var(--mhop-text-sub);
}
.panel {
  border-radius: 14px;
}
.todo-list {
  list-style: none;
  margin: 0;
  padding: 0;
}
.todo-list li {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 6px;
  border-bottom: 1px dashed #ece7dd;
  cursor: pointer;
}
.todo-list li:last-child {
  border-bottom: none;
}
.todo-list .num {
  font-size: 18px;
  font-weight: 700;
  margin-left: auto;
}
.todo-list .num.danger {
  color: var(--mhop-danger);
}
.todo-list .arrow {
  color: #c2bdb2;
}
</style>
