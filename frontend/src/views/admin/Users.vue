<template>
  <div v-loading="loading">
    <h2 class="page-title">用户管理</h2>
    <el-card shadow="never">
      <el-table :data="users" stripe>
        <el-table-column label="ID" prop="id" min-width="80" />
        <el-table-column label="用户名" prop="username" min-width="160" />
        <el-table-column label="角色" min-width="120">
          <template #default="{ row }">
            <el-tag :type="row.role === 'admin' ? 'danger' : 'info'" effect="plain" size="small">
              {{ row.role === 'admin' ? '管理员' : '普通用户' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" min-width="170">
          <template #default="{ row }">
            <el-tag v-if="row.id === auth.user?.id" type="info" effect="plain" size="small">
              当前登录账号
            </el-tag>
            <el-switch
              v-else
              :model-value="row.status === 'active'"
              active-text="正常"
              inactive-text="已停用"
              inline-prompt
              @change="(v) => toggle(row, v)"
            />
          </template>
        </el-table-column>
        <el-table-column label="注册时间" min-width="180">
          <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'
import { fmtTime } from '../../utils/format'

const auth = useAuthStore()
const users = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    users.value = await http.get('/admin/users')
  } finally {
    loading.value = false
  }
}

async function toggle(row, active) {
  const status = active ? 'active' : 'disabled'
  try {
    await ElMessageBox.confirm(
      `确定${active ? '恢复' : '停用'}用户「${row.username}」吗？${active ? '' : '停用后该用户将无法登录。'}`,
      '账号状态',
      { type: 'warning' }
    )
    await http.post(`/admin/users/${row.id}/status`, { status })
    row.status = status
    ElMessage.success('已更新')
  } catch {
    load()
  }
}

onMounted(load)
</script>

<style scoped>
.page-title {
  margin: 0 0 14px;
  font-size: 20px;
}
</style>
