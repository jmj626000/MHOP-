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
        <el-menu-item index="/admin/dashboard">
          <el-icon><DataBoard /></el-icon><span>数据看板</span>
        </el-menu-item>
        <el-menu-item index="/admin/review">
          <el-icon><Checked /></el-icon><span>内容审核</span>
        </el-menu-item>
        <el-menu-item index="/admin/users">
          <el-icon><UserFilled /></el-icon><span>用户管理</span>
        </el-menu-item>
        <el-menu-item index="/admin/ai-logs">
          <el-icon><MagicStick /></el-icon><span>AI 交互日志</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="admin-header">
        <div>
          <router-link to="/" class="back-site"><el-icon><Monitor /></el-icon> 访问前台</router-link>
        </div>
        <el-dropdown @command="onCommand">
          <span class="user-trigger">
            <el-icon><Avatar /></el-icon>
            {{ auth.displayName }}（管理员）
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
  </el-container>
</template>

<script setup>
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

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
}
.admin-main {
  background: #f4f2ed;
  padding: 22px;
}

/* 移动端：侧边栏变为顶部横向滚动菜单 */
@media (max-width: 760px) {
  .admin-shell {
    height: auto;
    min-height: 100vh;
    flex-direction: column;
  }
  .admin-aside {
    width: 100% !important;
    height: auto !important;
  }
  .logo {
    padding: 12px 14px;
  }
  .logo small {
    display: none;
  }
  .admin-aside :deep(.el-menu) {
    display: flex;
    overflow-x: auto;
    scrollbar-width: none;
  }
  .admin-aside :deep(.el-menu::-webkit-scrollbar) {
    display: none;
  }
  .admin-aside :deep(.el-menu-item) {
    flex: none;
    margin: 4px 6px;
    white-space: nowrap;
  }
  .admin-header {
    height: auto !important;
    min-height: 50px;
    padding: 8px 14px;
    gap: 10px;
    flex-wrap: wrap;
  }
  .admin-main {
    padding: 14px 10px;
  }
}
</style>
