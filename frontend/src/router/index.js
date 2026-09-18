import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/',
    component: () => import('../layouts/UserLayout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('../views/user/HomeView.vue') },
      { path: 'forum', name: 'forum', component: () => import('../views/user/ForumView.vue') },
      { path: 'forum/:id', name: 'post-detail', component: () => import('../views/user/PostDetailView.vue') },
      { path: 'assessment', name: 'assessment', component: () => import('../views/user/AssessmentView.vue') },
      { path: 'login', name: 'login', component: () => import('../views/user/LoginView.vue') },
      { path: 'register', name: 'register', component: () => import('../views/user/RegisterView.vue') },
    ],
  },
  { path: '/admin/login', name: 'admin-login', component: () => import('../views/admin/AdminLogin.vue') },
  {
    path: '/admin',
    component: () => import('../layouts/AdminLayout.vue'),
    meta: { requiresAdmin: true },
    children: [
      { path: '', redirect: '/admin/dashboard' },
      { path: 'dashboard', name: 'admin-dashboard', component: () => import('../views/admin/Dashboard.vue') },
      { path: 'review', name: 'admin-review', component: () => import('../views/admin/Review.vue') },
      { path: 'users', name: 'admin-users', component: () => import('../views/admin/Users.vue') },
      { path: 'ai-logs', name: 'admin-logs', component: () => import('../views/admin/AiLogs.vue') },
    ],
  },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 }
  },
})

router.beforeEach((to) => {
  if (to.meta.requiresAdmin) {
    const auth = useAuthStore()
    if (!auth.isAdmin) return { name: 'admin-login' }
  }
  return true
})

export default router
