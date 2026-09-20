<template>
  <div v-loading="loading">
    <h2 class="page-title">用户管理</h2>
    <el-card shadow="never">
      <el-table :data="users" stripe>
        <el-table-column label="ID" prop="id" min-width="80" />
        <el-table-column label="用户名" min-width="160">
          <template #default="{ row }">
            <span>{{ row.username }}</span>
            <el-tag v-if="row.badge" size="small" type="success" effect="dark" style="margin-left: 6px">{{ row.badge }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="邮箱" min-width="180">
          <template #default="{ row }">{{ row.email || '—' }}</template>
        </el-table-column>
        <el-table-column label="角色" min-width="120">
          <template #default="{ row }">
            <el-tag :type="row.role === 'admin' ? 'danger' : 'info'" effect="plain" size="small">
              {{ row.role === 'admin' ? '管理员' : '普通用户' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" min-width="140">
          <template #default="{ row }">
            <el-tag v-if="row.id === auth.user?.id" type="info" effect="plain" size="small">当前账号</el-tag>
            <el-switch
              v-else
              :model-value="row.status === 'active'"
              active-text="正常"
              inactive-text="停用"
              inline-prompt
              @change="(v) => toggleStatus(row, v)"
            />
          </template>
        </el-table-column>
        <el-table-column label="注册时间" min-width="170">
          <template #default="{ row }">{{ fmtTime(row.created_at) }}</template>
        </el-table-column>
        <el-table-column label="操作" min-width="280" fixed="right">
          <template #default="{ row }">
            <el-button size="small" @click="openBadgeDialog(row)">标识</el-button>
            <el-button v-if="row.role !== 'admin'" size="small" type="warning" plain @click="promote(row)">设为管理员</el-button>
            <el-button v-else-if="row.id !== auth.user?.id" size="small" type="info" plain @click="demote(row)">取消管理员</el-button>
            <el-button size="small" @click="openResetDialog(row)">重置密码</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 标识设置弹窗 -->
    <el-dialog v-model="badgeDialog.visible" title="设置用户标识" width="420px">
      <p style="margin-bottom: 12px; color: #606266">
        为「{{ badgeDialog.username }}」设置标识标签，如：认证咨询师、志愿者、心理导师等。留空则清除标识。
      </p>
      <el-input v-model="badgeDialog.badge" placeholder="输入标识（最多64字符）" maxlength="64" show-word-limit />
      <template #footer>
        <el-button @click="badgeDialog.visible = false">取消</el-button>
        <el-button type="primary" :loading="badgeDialog.saving" @click="saveBadge">保存</el-button>
      </template>
    </el-dialog>

    <!-- 重置密码弹窗 -->
    <el-dialog v-model="resetDialog.visible" title="重置用户密码" width="420px">
      <p style="margin-bottom: 12px; color: #606266">
        为「{{ resetDialog.username }}」设置新密码（至少 6 位）。
      </p>
      <el-input v-model="resetDialog.password" type="password" placeholder="新密码" show-password />
      <template #footer>
        <el-button @click="resetDialog.visible = false">取消</el-button>
        <el-button type="primary" :loading="resetDialog.saving" @click="saveReset">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'
import { fmtTime } from '../../utils/format'

const auth = useAuthStore()
const users = ref([])
const loading = ref(false)

const badgeDialog = reactive({ visible: false, userId: 0, username: '', badge: '', saving: false })
const resetDialog = reactive({ visible: false, userId: 0, username: '', password: '', saving: false })

async function load() {
  loading.value = true
  try {
    users.value = await http.get('/admin/users')
  } finally {
    loading.value = false
  }
}

async function toggleStatus(row, active) {
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

async function promote(row) {
  try {
    await ElMessageBox.confirm(`确定将「${row.username}」提升为管理员吗？`, '权限管理', { type: 'warning' })
    await http.post(`/admin/users/${row.id}/role`, { action: 'promote' })
    ElMessage.success('已设为管理员')
    load()
  } catch { /* cancelled */ }
}

async function demote(row) {
  try {
    await ElMessageBox.confirm(`确定取消「${row.username}」的管理员权限吗？`, '权限管理', { type: 'warning' })
    await http.post(`/admin/users/${row.id}/role`, { action: 'demote' })
    ElMessage.success('已取消管理员权限')
    load()
  } catch { /* cancelled */ }
}

function openBadgeDialog(row) {
  badgeDialog.userId = row.id
  badgeDialog.username = row.username
  badgeDialog.badge = row.badge || ''
  badgeDialog.visible = true
}

async function saveBadge() {
  badgeDialog.saving = true
  try {
    await http.post(`/admin/users/${badgeDialog.userId}/badge`, { badge: badgeDialog.badge })
    ElMessage.success('标识已更新')
    badgeDialog.visible = false
    load()
  } finally {
    badgeDialog.saving = false
  }
}

function openResetDialog(row) {
  resetDialog.userId = row.id
  resetDialog.username = row.username
  resetDialog.password = ''
  resetDialog.visible = true
}

async function saveReset() {
  if (resetDialog.password.length < 6) {
    ElMessage.warning('密码至少 6 位')
    return
  }
  resetDialog.saving = true
  try {
    await http.post(`/admin/users/${resetDialog.userId}/reset-password`, { password: resetDialog.password })
    ElMessage.success('密码已重置')
    resetDialog.visible = false
  } finally {
    resetDialog.saving = false
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
