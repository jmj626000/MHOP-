<template>
  <div class="auth-wrap">
    <div class="auth-card mhop-card">
      <h2>欢迎回来</h2>
      <p class="text-sub">登录后可选择实名发布、云端保存评估记录</p>

      <el-radio-group v-model="mode" class="mode-switch">
        <el-radio-button value="password">密码登录</el-radio-button>
        <el-radio-button value="email">邮箱验证码</el-radio-button>
      </el-radio-group>

      <!-- 密码登录 -->
      <el-form v-if="mode === 'password'" :model="form" :rules="rules" ref="formRef" label-position="top" @submit.prevent>
        <el-form-item label="用户名" prop="username">
          <el-input v-model="form.username" size="large" placeholder="请输入用户名" :prefix-icon="User" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" size="large" type="password" show-password placeholder="请输入密码"
            :prefix-icon="Lock" @keyup.enter="submit" />
        </el-form-item>
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" round @click="submit">登 录</el-button>
      </el-form>

      <!-- 邮箱验证码登录 -->
      <el-form v-else :model="mailForm" :rules="mailRules" ref="mailFormRef" label-position="top" @submit.prevent>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="mailForm.email" size="large" placeholder="请输入邮箱地址" :prefix-icon="Message"
            @keyup.enter="sendCode" />
        </el-form-item>
        <el-form-item label="验证码" prop="code">
          <div class="code-row">
            <el-input v-model="mailForm.code" size="large" maxlength="6" placeholder="6 位验证码" :prefix-icon="Key"
              @keyup.enter="submitEmail" />
            <el-button size="large" :disabled="countdown > 0" :loading="sending" class="code-btn"
              @click="sendCode">
              {{ countdown > 0 ? `${countdown}s 后重发` : '发送验证码' }}
            </el-button>
          </div>
        </el-form-item>
        <el-alert v-if="devCode" type="warning" :closable="false" class="dev-tip"
          title="开发模式：SMTP 未配置，本次验证码直接显示"
          :description="`验证码为 ${devCode}（10 分钟内有效）。配置 SMTP 后将发送到真实邮箱。`" />
        <el-button type="primary" size="large" style="width: 100%" :loading="loading" round @click="submitEmail">
          登录 / 注册
        </el-button>
        <p class="mail-note">未注册过的邮箱验证通过后将自动创建账号</p>
      </el-form>

      <div class="auth-foot">
        还没有账号？<router-link to="/register">立即注册</router-link>
        <span style="float: right"><router-link to="/admin/login">管理员入口</router-link></span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Key, Lock, Message, User } from '@element-plus/icons-vue'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const mode = ref('password')
const loading = ref(false)
const formRef = ref()
const form = reactive({ username: '', password: '' })
const rules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

// ---- 邮箱验证码 ----
const mailFormRef = ref()
const sending = ref(false)
const countdown = ref(0)
const devCode = ref('')
let timer = null
const mailForm = reactive({ email: '', code: '' })
const mailRules = {
  email: [
    { required: true, message: '请输入邮箱', trigger: 'blur' },
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' },
  ],
  code: [
    { required: true, message: '请输入验证码', trigger: 'blur' },
    { pattern: /^\d{6}$/, message: '验证码为 6 位数字', trigger: 'blur' },
  ],
}

function startCountdown(seconds) {
  countdown.value = seconds
  timer = setInterval(() => {
    countdown.value -= 1
    if (countdown.value <= 0) {
      clearInterval(timer)
      timer = null
    }
  }, 1000)
}

onBeforeUnmount(() => { if (timer) clearInterval(timer) })

async function sendCode() {
  await mailFormRef.value.validateField('email')
  sending.value = true
  devCode.value = ''
  try {
    const res = await auth.sendEmailCode(mailForm.email.trim())
    ElMessage.success('验证码已发送，请查收邮箱（10 分钟内有效）')
    if (res.dev_mode && res.dev_code) devCode.value = res.dev_code
    startCountdown(res.resend_after || 60)
  } finally {
    sending.value = false
  }
}

async function submit() {
  await formRef.value.validate()
  loading.value = true
  try {
    const user = await auth.login(form.username.trim(), form.password)
    ElMessage.success('登录成功')
    if (user.role === 'admin') router.push('/admin/dashboard')
    else router.push(route.query.redirect || '/forum')
  } finally {
    loading.value = false
  }
}

async function submitEmail() {
  await mailFormRef.value.validate()
  loading.value = true
  try {
    const { newAccount } = await auth.loginByEmail(mailForm.email.trim().toLowerCase(), mailForm.code.trim())
    ElMessage.success(newAccount ? '验证通过，已为你自动注册并登录' : '登录成功')
    router.push(route.query.redirect || '/forum')
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
  margin: 0 0 20px;
  font-size: 13.5px;
}
.mode-switch {
  width: 100%;
  margin-bottom: 20px;
  display: flex;
}
.mode-switch :deep(.el-radio-button) {
  width: 50%;
}
.mode-switch :deep(.el-radio-button__inner) {
  width: 100%;
}
.code-row {
  display: flex;
  gap: 10px;
  width: 100%;
}
.code-btn {
  flex: 0 0 128px;
}
.dev-tip {
  margin: -6px 0 16px;
}
.mail-note {
  margin: 10px 0 0;
  font-size: 12.5px;
  color: var(--el-text-color-secondary);
  text-align: center;
}
.auth-foot {
  margin-top: 18px;
  font-size: 13.5px;
}
.auth-foot a {
  color: var(--mhop-teal);
  font-weight: 600;
}
@media (max-width: 640px) {
  .auth-wrap {
    margin: 20px auto;
  }
  .auth-card {
    padding: 26px 20px 22px;
  }
  .code-btn {
    flex-basis: 112px;
  }
}
</style>
