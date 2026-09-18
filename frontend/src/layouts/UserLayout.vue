<template>
  <div style="min-height: 100vh; display: flex; flex-direction: column">
    <header class="nav-header">
      <div class="mhop-container nav-inner">
        <router-link to="/" class="brand">
          <span class="brand-mark"><el-icon><Sunny /></el-icon></span>
          <span>
            <strong>心光 MHOP</strong>
            <small>公益心理辅助平台</small>
          </span>
        </router-link>
        <nav class="nav-links">
          <router-link to="/">首页</router-link>
          <router-link to="/forum">互助论坛</router-link>
          <router-link to="/assessment">AI 心理评估</router-link>
        </nav>
        <div class="nav-right">
          <el-tag type="success" effect="light" round>
            <el-icon style="vertical-align: -2px"><Connection /></el-icon>
            {{ online.count }} 人在线
          </el-tag>
          <template v-if="auth.isLoggedIn">
            <el-dropdown @command="onCommand">
              <span class="user-trigger">
                <el-icon><UserFilled /></el-icon>
                {{ auth.displayName }}
                <el-icon><ArrowDown /></el-icon>
              </span>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-if="auth.isAdmin" command="admin">
                    <el-icon><Setting /></el-icon>管理后台
                  </el-dropdown-item>
                  <el-dropdown-item command="logout">
                    <el-icon><SwitchButton /></el-icon>退出登录
                  </el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </template>
          <template v-else>
            <router-link to="/login"><el-button text>登录</el-button></router-link>
            <router-link to="/register"><el-button type="primary" round>注册</el-button></router-link>
          </template>
        </div>
      </div>
    </header>

    <EmergencyBanner />

    <main class="mhop-container" style="flex: 1; width: 100%; padding-top: 22px; padding-bottom: 40px">
      <router-view v-slot="{ Component }">
        <transition name="page-fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>

    <footer class="site-footer">
      <div class="mhop-container">
        <p>心光 MHOP · 全国性匿名优先公益心理辅助平台</p>
        <p class="text-sub" style="font-size: 12.5px; line-height: 1.8">
          本平台提供的 AI 评估与回复仅供心理自助参考，不能替代专业医学诊断与治疗。若症状持续或加重，请及时前往正规医疗机构就诊。
          <br />紧急情况请立即拨打 12356 / 010-82951332 / 110 / 120。
          <router-link to="/admin/login" style="margin-left: 8px">运营入口</router-link>
        </p>
      </div>
    </footer>
  </div>
</template>

<script setup>
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import EmergencyBanner from '../components/EmergencyBanner.vue'
import { useAuthStore } from '../stores/auth'
import { useOnlineStore } from '../stores/online'

const router = useRouter()
const auth = useAuthStore()
const online = useOnlineStore()

function onCommand(cmd) {
  if (cmd === 'admin') router.push('/admin/dashboard')
  if (cmd === 'logout') {
    ElMessageBox.confirm('确定退出登录吗？', '提示', { type: 'warning' })
      .then(() => {
        auth.logout()
        router.push('/')
      })
      .catch(() => {})
  }
}
</script>

<style scoped>
.nav-header {
  background: rgba(255, 255, 255, 0.92);
  backdrop-filter: blur(8px);
  border-bottom: 1px solid #e9e4da;
  position: sticky;
  top: 0;
  z-index: 100;
}
.nav-inner {
  height: 64px;
  display: flex;
  align-items: center;
  gap: 28px;
}
.brand {
  display: flex;
  align-items: center;
  gap: 10px;
}
.brand-mark {
  width: 38px;
  height: 38px;
  border-radius: 12px;
  background: linear-gradient(135deg, #2f8f83, #5eaaa1);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}
.brand strong {
  display: block;
  font-size: 17px;
  line-height: 1.2;
}
.brand small {
  color: var(--mhop-text-sub);
  font-size: 11.5px;
}
.nav-links {
  display: flex;
  gap: 24px;
  font-size: 15px;
}
.nav-links a {
  color: var(--mhop-text-sub);
  padding: 4px 2px;
  border-bottom: 2px solid transparent;
  transition: all 0.15s;
}
.nav-links a.router-link-exact-active {
  color: var(--mhop-teal);
  font-weight: 600;
  border-bottom-color: var(--mhop-teal);
}
.nav-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 14px;
}
.user-trigger {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  cursor: pointer;
  color: var(--mhop-text);
  outline: none;
}
.site-footer {
  background: #efebe2;
  border-top: 1px solid #e3ddd1;
  padding: 22px 0;
  text-align: center;
  font-size: 13.5px;
}
.site-footer p {
  margin: 4px 0;
}
.page-fade-enter-active,
.page-fade-leave-active {
  transition: opacity 0.18s ease;
}
.page-fade-enter-from,
.page-fade-leave-to {
  opacity: 0;
}
</style>
