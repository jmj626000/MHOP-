<template>
  <div class="profile-page">
    <div class="mhop-card profile-card">
      <h2>个人主页</h2>
      <div class="profile-body">
        <div class="avatar-section">
          <div class="avatar-wrap" @click="pickAvatar">
            <img v-if="avatarUrl" :src="avatarUrl" class="avatar-img" alt="头像" />
            <span v-else class="avatar-placeholder">{{ form.username.charAt(0).toUpperCase() }}</span>
            <span class="avatar-overlay">更换</span>
          </div>
          <input ref="avatarInput" type="file" accept="image/jpeg,image/png,image/webp,image/gif" style="display:none" @change="onAvatarChange" />
        </div>
        <div class="info-section">
          <el-form label-position="top" @submit.prevent>
            <el-form-item label="用户名">
              <el-input v-model="form.username" placeholder="用户名" maxlength="32" show-word-limit />
            </el-form-item>
            <el-form-item label="邮箱">
              <el-input :model-value="email || '未绑定'" disabled />
            </el-form-item>
            <el-form-item label="角色">
              <el-tag :type="isAdmin ? 'danger' : 'info'">{{ isAdmin ? '管理员' : '普通用户' }}</el-tag>
            </el-form-item>
            <el-form-item label="注册时间">
              <span class="text-sub">{{ createdAt }}</span>
            </el-form-item>
          </el-form>
          <el-button type="primary" round :loading="saving" @click="save">保存修改</el-button>
        </div>
      </div>
      <div class="my-stats" v-if="stats">
        <span>发帖 {{ stats.posts }}</span>
        <span>回复 {{ stats.replies }}</span>
        <span>获赞 {{ stats.likes }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const form = ref({ username: '' })
const avatarUrl = ref('')
const email = ref('')
const createdAt = ref('')
const saving = ref(false)
const avatarInput = ref(null)
const isAdmin = computed(() => auth.user?.role === 'admin')
const stats = ref(null)

onMounted(async () => {
  form.value.username = auth.user?.username || ''
  avatarUrl.value = auth.user?.avatar || ''
  email.value = auth.user?.email || ''
  createdAt.value = auth.user?.created_at ? new Date(auth.user.created_at).toLocaleString('zh-CN') : ''
  try {
    stats.value = await http.get('/forum/stats')
  } catch { /* ignore */ }
})

function pickAvatar() {
  avatarInput.value?.click()
}

async function onAvatarChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('图片不能超过 5MB')
    return
  }
  try {
    const fd = new FormData()
    fd.append('file', file)
    const data = await http.post('/upload/avatar', fd, { headers: { 'Content-Type': 'multipart/form-data' } })
    avatarUrl.value = data.url
    ElMessage.success('头像已上传，点击保存修改生效')
  } catch {
    /* error toast handled by interceptor */
  }
  e.target.value = ''
}

async function save() {
  if (form.value.username.trim().length < 2) {
    ElMessage.warning('用户名至少 2 个字符')
    return
  }
  saving.value = true
  try {
    const updated = await http.put('/auth/profile', {
      username: form.value.username.trim(),
      avatar: avatarUrl.value,
    })
    auth.user = updated
    localStorage.setItem('mhop_user', JSON.stringify(updated))
    ElMessage.success('资料已更新')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.profile-page {
  max-width: 600px;
  margin: 24px auto;
}
.profile-card h2 {
  margin-bottom: 20px;
}
.profile-body {
  display: flex;
  gap: 28px;
  align-items: flex-start;
}
.avatar-section {
  flex-shrink: 0;
}
.avatar-wrap {
  width: 96px;
  height: 96px;
  border-radius: 50%;
  overflow: hidden;
  cursor: pointer;
  position: relative;
  background: var(--mhop-primary, #5b8def);
  display: flex;
  align-items: center;
  justify-content: center;
}
.avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.avatar-placeholder {
  font-size: 36px;
  color: #fff;
  font-weight: 600;
}
.avatar-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  background: rgba(0,0,0,0.5);
  color: #fff;
  text-align: center;
  font-size: 12px;
  padding: 2px 0;
  opacity: 0;
  transition: opacity 0.2s;
}
.avatar-wrap:hover .avatar-overlay {
  opacity: 1;
}
.info-section {
  flex: 1;
  min-width: 0;
}
.my-stats {
  display: flex;
  gap: 24px;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid var(--mhop-border, #e8e8e8);
  color: var(--mhop-text, #333);
  font-size: 14px;
}
@media (max-width: 640px) {
  .profile-body { flex-direction: column; align-items: center; }
  .profile-page { margin: 12px; }
}
</style>
