<template>
  <el-container class="admin-shell">
    <el-aside width="220px" class="admin-aside">
      <div class="logo">
        <el-icon :size="22"><Sunny /></el-icon>
        <div>
          <strong>心光 MHOP</strong>
          <small>运营管理后台</small>
        </div>
      </div>
      <el-menu :default-active="route.path" router background-color="transparent" text-color="#cfe4e0"
        active-text-color="#ffffff">
        <el-menu-item v-for="item in menus" :key="item.path" :index="item.path">
          <el-icon><component :is="item.icon" /></el-icon><span>{{ item.name }}</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="admin-header">
        <div class="header-left">
          <span class="admin-burger" role="button" aria-label="打开菜单" @click="drawer = true">
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor"
              stroke-width="2.2" stroke-linecap="round">
              <line x1="3.5" y1="6.5" x2="20.5" y2="6.5" />
              <line x1="3.5" y1="12" x2="20.5" y2="12" />
              <line x1="3.5" y1="17.5" x2="20.5" y2="17.5" />
            </svg>
          </span>
          <strong class="brand-mini">心光 MHOP 后台</strong>
          <router-link to="/" class="back-site"><el-icon><Monitor /></el-icon> 访问前台</router-link>
        </div>
        <el-dropdown @command="onCommand">
          <span class="user-trigger">
            <el-icon><Avatar /></el-icon>
            {{ auth.displayName }}<span class="admin-role">（管理员）</span>
            <el-icon><ArrowDown /></el-icon>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="site"><el-icon><Monitor /></el-icon>返回前台</el-dropdown-item>
              <el-dropdown-item command="logout"><el-icon><SwitchButton /></el-icon>退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>
      <el-main class="admin-main">
        <router-view />
      </el-main>
    </el-container>

    <!-- 移动端抽屉菜单（桌面端隐藏侧栏，不显示汉堡） -->
    <el-drawer v-model="drawer" direction="ltr" size="72%" :with-header="false" class="admin-drawer">
      <div class="drawer-brand">
        <el-icon :size="22"><Sunny /></el-icon>
        <div>
          <strong>心光 MHOP</strong>
          <small>运营管理后台</small>
        </div>
      </div>
      <nav class="drawer-nav">
        <a v-for="item in menus" :key="item.path"
          :class="['drawer-nav-item', { active: route.path === item.path }]"
          @click="go(item.path)">
          <el-icon><component :is="item.icon" /></el-icon>
          <span>{{ item.name }}</span>
        </a>
      </nav>
    </el-drawer>
  </el-container>
</template>

<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const drawer = ref(false)

const menus = [
  { path: '/admin/dashboard', name: '数据看板', icon: 'DataBoard' },
  { path: '/admin/review', name: '内容审核', icon: 'Checked' },
  { path: '/admin/users', name: '用户管理', icon: 'UserFilled' },
  { path: '/admin/ai-logs', name: 'AI 交互日志', icon: 'MagicStick' },
]

function go(path) {
  drawer.value = false
  router.push(path)
}

function onCommand(cmd) {
  if (cmd === 'site' || cmd === 'logout') {
    if (cmd === 'logout') auth.logout()
    router.push('/')
  }
}
</script>

<style scoped>
.admin-shell {
  height: 100vh;
}
.admin-aside {
  background: linear-gradient(180deg, #206b62 0%, #18514b 100%);
  display: flex;
  flex-direction: column;
}
.logo {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #fff;
  padding: 20px 18px 16px;
}
.logo strong {
  display: block;
  font-size: 16px;
}
.logo small {
  color: #a9cfc9;
  font-size: 11.5px;
}
.admin-aside :deep(.el-menu) {
  border-right: none;
}
.admin-aside :deep(.el-menu-item.is-active) {
  background: rgba(255, 255, 255, 0.14);
  border-radius: 8px;
  margin: 4px 10px;
}
.admin-aside :deep(.el-menu-item) {
  border-radius: 8px;
  margin: 4px 10px;
}
.admin-header {
  background: #fff;
  border-bottom: 1px solid #ebe6dc;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}
.brand-mini {
  display: none;
  font-size: 15.5px;
  color: var(--mhop-text);
  white-space: nowrap;
}
.admin-burger {
  display: none;
  color: var(--mhop-text);
  cursor: pointer;
  padding: 4px;
  line-height: 0;
}
.back-site {
  color: var(--mhop-text-sub);
  font-size: 13.5px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.user-trigger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  outline: none;
  font-size: 14px;
  white-space: nowrap;
}
.admin-main {
  background: #f4f2ed;
  padding: 22px;
}

/* 移动端：侧栏收进抽屉，顶栏放汉堡 */
@media (max-width: 760px) {
  .admin-shell {
    height: auto;
    min-height: 100vh;
  }
  .admin-aside {
    display: none;
  }
  .admin-burger {
    display: inline-flex;
  }
  .brand-mini {
    display: block;
  }
  .back-site {
    display: none;
  }
  .admin-role {
    display: none;
  }
  .admin-header {
    height: 54px !important;
    padding: 0 12px;
  }
  .admin-main {
    padding: 14px 10px;
  }
}
</style>

<!-- 抽屉被 teleport 到 body，scoped 样式选不中，使用全局样式块 -->
<style>
.admin-drawer.el-drawer {
  background: linear-gradient(180deg, #206b62 0%, #18514b 100%);
}
.admin-drawer .el-drawer__body {
  padding: 0;
  overflow-y: auto;
}
.admin-drawer .drawer-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #fff;
  padding: 22px 18px 14px;
}
.admin-drawer .drawer-brand strong {
  display: block;
  font-size: 16px;
}
.admin-drawer .drawer-brand small {
  color: #a9cfc9;
  font-size: 11.5px;
}
.admin-drawer .drawer-nav {
  display: flex;
  flex-direction: column;
  padding: 6px 10px;
}
.admin-drawer .drawer-nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 13px 12px;
  margin: 3px 0;
  border-radius: 8px;
  color: #cfe4e0;
  font-size: 15.5px;
  cursor: pointer;
}
.admin-drawer .drawer-nav-item.active {
  background: rgba(255, 255, 255, 0.14);
  color: #ffffff;
  font-weight: 600;
}
</style>
