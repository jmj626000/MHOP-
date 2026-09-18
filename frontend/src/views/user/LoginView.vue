<template>
  <div class="auth-wrap">
    <div class="auth-card mhop-card">
      <h2>欢迎回来</h2>
      <p class="text-sub">登录后可选择实名发布、云端保存评估记录</p>
      <el-form :model="form" :rules="rules" ref="formRef" label-position="top" @submit.prevent>
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" size="large" placeholder="请输入用户名" :prefix-icon="User" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" size="large" type="password" show-password placeholder="请输入密码"
            :prefix-icon="Lock" @keyup.enter="submit" />
        </el-form-item>
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" round @click="submit">登 录</el-button>
      </el-form>
      <div class="auth-foot">
        还没有账号？<router-link to="/register">立即注册</router-link>
        <span style="float: right"><router-link to="/admin/login">管理员入口</router-link></span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Lock, User } from '@element-plus/icons-vue'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const formRef = ref()
const loading = ref(false)
const form = ref({ username: '', password: '' })
const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function submit() {
  await formRef.value.validate()
  loading.value = true
  try {
    const user = await auth.login(form.value.username.trim(), form.value.password)
    ElMessage.success('登录成功')
    if (user.role === 'admin') router.push('/admin/dashboard')
    else router.push(route.query.redirect || '/forum')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-wrap {
  max-width: 430px;
  margin: 50px auto;
  padding: 0 16px;
}
.auth-card {
  padding: 38px 36px 30px;
}
.auth-card h2 {
  margin: 0 0 6px;
}
.auth-card > p {
  margin: 0 0 24px;
  font-size: 13.5px;
}
.auth-foot {
  margin-top: 18px;
  font-size: 13.5px;
}
.auth-foot a {
  color: var(--mhop-teal);
  font-weight: 600;
}
</style>
