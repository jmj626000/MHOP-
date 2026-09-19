<template>
  <div class="login-shell">
    <div class="login-card">
      <div class="login-brand">
        <el-icon :size="34"><Sunny /></el-icon>
        <h2>心光 MHOP · 运营管理后台</h2>
        <p>内容审核 · 用户管理 · 数据看板</p>
      </div>
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top" @submit.prevent>
        <el-form-item label="管理员账号" prop="username">
          <el-input v-model="form.username" size="large" :prefix-icon="User" placeholder="请输入管理员账号" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" size="large" type="password" show-password :prefix-icon="Lock"
            placeholder="请输入密码" @keyup.enter="submit" />
        </el-form-item>
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" round @click="submit">
          进入后台
        </el-button>
      </el-form>
      <div style="text-align: center; margin-top: 14px">
        <router-link to="/" style="color: #9fc4be; font-size: 13px">← 返回前台首页</router-link>
      </div>
      <p class="hint">默认管理员：admin / admin123（首次登录后请修改密码）</p>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Lock, User } from '@element-plus/icons-vue'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const formRef = ref()
const loading = ref(false)
const form = ref({ username: '', password: '' })
const rules = {
  username: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function submit() {
  await formRef.value.validate()
  loading.value = true
  try {
    const user = await auth.login(form.value.username.trim(), form.value.password)
    if (user.role !== 'admin') {
      ElMessage.error('该账号不是管理员')
      auth.logout()
      return
    }
    ElMessage.success('登录成功')
    router.push('/admin/dashboard')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-shell {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #18514b 0%, #2f8f83 100%);
}
.login-card {
  width: 420px;
  max-width: calc(100vw - 28px);
  background: rgba(255, 255, 255, 0.97);
  border-radius: 18px;
  padding: 40px 38px 26px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.22);
}
@media (max-width: 640px) {
  .login-card {
    padding: 28px 22px 22px;
  }
}
.login-brand {
  text-align: center;
  color: #227067;
  margin-bottom: 24px;
}
.login-brand h2 {
  margin: 10px 0 4px;
  font-size: 19px;
}
.login-brand p {
  color: var(--mhop-text-sub);
  font-size: 13px;
  margin: 0;
}
.hint {
  text-align: center;
  color: #a0978a;
  font-size: 12px;
  margin-top: 14px;
}
</style>
