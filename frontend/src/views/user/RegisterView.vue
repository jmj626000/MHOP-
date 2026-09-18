<template>
  <div class="auth-wrap">
    <div class="auth-card mhop-card">
      <h2>创建账号</h2>
      <p class="text-sub">平台匿名优先，即使登录，发布时仍可随时选择匿名</p>
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top" @submit.prevent>
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" size="large" placeholder="2-32 个字符" :prefix-icon="User" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" size="large" type="password" show-password placeholder="至少 6 位"
            :prefix-icon="Lock" />
        </el-form-item>
        <el-form-item label="确认密码" prop="confirm">
          <el-input v-model="form.confirm" size="large" type="password" show-password placeholder="再次输入密码"
            :prefix-icon="Lock" @keyup.enter="submit" />
        </el-form-item>
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" round @click="submit">注 册</el-button>
      </el-form>
      <div class="auth-foot">
        已有账号？<router-link to="/login">去登录</router-link>
      </div>
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
const form = ref({ username: '', password: '', confirm: '' })

const rules = {
  username: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 2, max: 32, message: '长度为 2-32 个字符', trigger: 'blur' },
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
  confirm: [
    { required: true, message: '请再次输入密码', trigger: 'blur' },
    {
      validator: (_r, v, cb) => (v === form.value.password ? cb() : cb(new Error('两次输入的密码不一致'))),
      trigger: 'blur',
    },
  ],
}

async function submit() {
  await formRef.value.validate()
  loading.value = true
  try {
    await auth.register(form.value.username.trim(), form.value.password)
    ElMessage.success('注册成功，已自动登录')
    router.push('/forum')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-wrap {
  max-width: 430px;
  margin: 40px auto;
  padding: 0 16px;
}
.auth-card {
  padding: 36px;
}
.auth-card h2 {
  margin: 0 0 6px;
}
.auth-card > p {
  margin: 0 0 22px;
  font-size: 13.5px;
}
.auth-foot {
  margin-top: 16px;
  font-size: 13.5px;
}
.auth-foot a {
  color: var(--mhop-teal);
  font-weight: 600;
}
</style>
