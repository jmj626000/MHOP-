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
          <el-tag type="success" effect="light" round class="online-tag">
            <el-icon style="vertical-align: -2px"><Connection /></el-icon>
            {{ online.count }} 人在线
          </el-tag>
          <template v-if="auth.isLoggedIn">
            <el-dropdown @command="onCommand" class="user-dropdown">
              <span class="user-trigger">
                <img v-if="auth.user?.avatar" :src="auth.user.avatar" class="nav-avatar" />
                <el-icon v-else><UserFilled /></el-icon>
                {{ auth.displayName }}
                <el-icon><ArrowDown /></el-icon>
              </span>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="profile">
                    <el-icon><User /></el-icon>个人主页
                  </el-dropdown-item>
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
            <router-link to="/login" class="login-link"><el-button text>登录</el-button></router-link>
            <router-link to="/register" class="register-link"><el-button type="primary" round>注册</el-button></router-link>
          </template>
          <span class="nav-burger" @click="drawer = true" role="button" aria-label="打开菜单">
            <svg viewBox="0 0 24 24" width="23" height="23" fill="none" stroke="currentColor"
              stroke-width="2.2" stroke-linecap="round">
              <line x1="3.5" y1="6.5" x2="20.5" y2="6.5" />
              <line x1="3.5" y1="12" x2="20.5" y2="12" />
              <line x1="3.5" y1="17.5" x2="20.5" y2="17.5" />
            </svg>
          </span>
        </div>
      </div>
    </header>

    <!-- 移动端抽屉菜单 -->
    <el-drawer v-model="drawer" title="菜单" direction="rtl" size="72%" class="nav-drawer">
      <div class="drawer-online">
        <el-icon style="color: #4e9e5f"><Connection /></el-icon>
        {{ online.count }} 人在线
      </div>
      <nav class="drawer-links" @click="drawer = false">
        <router-link to="/"><el-icon><HomeFilled /></el-icon> 首页</router-link>
        <router-link to="/forum"><el-icon><ChatLineSquare /></el-icon> 互助论坛</router-link>
        <router-link to="/assessment"><el-icon><DataAnalysis /></el-icon> AI 心理评估</router-link>
        <router-link v-if="auth.isLoggedIn" to="/profile"><el-icon><User /></el-icon> 个人主页</router-link>
      </nav>
      <div class="drawer-actions">
        <template v-if="auth.isLoggedIn">
          <el-button v-if="auth.isAdmin" round @click="go('/admin/dashboard')">
            <el-icon><Setting /></el-icon> 管理后台
          </el-button>
          <el-button type="danger" plain round @click="logoutMobile">
            <el-icon><SwitchButton /></el-icon> 退出登录
          </el-button>
          <p class="drawer-user">当前账号：{{ auth.displayName }}</p>
        </template>
        <template v-else>
          <el-button type="primary" round style="width: 100%" @click="go('/login')">登 录</el-button>
          <el-button round style="width: 100%" @click="go('/register')">注 册</el-button>
        </template>
      </div>
    </el-drawer>

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
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import EmergencyBanner from '../components/EmergencyBanner.vue'
import { useAuthStore } from '../stores/auth'
import { useOnlineStore } from '../stores/online'

const router = useRouter()
const auth = useAuthStore()
const online = useOnlineStore()
const drawer = ref(false)

function go(path) {
  drawer.value = false
  router.push(path)
}

function logoutMobile() {
  drawer.value = false
  auth.logout()
  router.push('/')
}

function onCommand(cmd) {
  if (cmd === 'profile') router.push('/profile')
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
.nav-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  object-fit: cover;
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

/* 汉堡按钮：桌面端隐藏 */
.nav-burger {
  display: none;
  color: var(--mhop-text);
  cursor: pointer;
  padding: 4px;
  line-height: 0;
}
.nav-burger svg {
  display: block;
}

/* 抽屉菜单 */
.drawer-online {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13.5px;
  color: var(--mhop-text-sub);
  padding: 0 4px 14px;
  border-bottom: 1px solid #eee9df;
}
.drawer-links {
  display: flex;
  flex-direction: column;
  padding: 10px 0;
}
.drawer-links a {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 8px;
  font-size: 16px;
  color: var(--mhop-text);
  border-radius: 10px;
}
.drawer-links a:active,
.drawer-links a.router-link-exact-active {
  background: var(--mhop-teal-light);
  color: var(--mhop-teal-dark);
  font-weight: 600;
}
.drawer-actions {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 8px;
}
.drawer-user {
  text-align: center;
  font-size: 12.5px;
  color: var(--mhop-text-sub);
  margin: 4px 0 0;
}

@media (max-width: 760px) {
  .nav-inner {
    height: 56px;
    gap: 12px;
  }
  .brand-mark {
    width: 34px;
    height: 34px;
    font-size: 18px;
  }
  .brand strong {
    font-size: 15.5px;
  }
  .brand small {
    display: none;
  }
  .nav-links,
  .online-tag,
  .user-dropdown,
  .login-link,
  .register-link {
    display: none;
  }
  .nav-burger {
    display: inline-block;
  }
  main.mhop-container {
    padding-top: 14px !important;
    padding-bottom: 28px !important;
  }
}
</style>
