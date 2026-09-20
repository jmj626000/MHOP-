import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/',
    component: () => import('../layouts/UserLayout.vue'),
    children: [
      { path: '', name: 'home', component: () => import('../views/user/HomeView.vue') },
      { path: 'forum', name: 'forum', component: () => import('../views/user/ForumView.vue'), meta: { requiresAuth: true, guestRedirect: 'home' } },
      { path: 'forum/:id', name: 'post-detail', component: () => import('../views/user/PostDetailView.vue'), meta: { requiresAuth: true, guestRedirect: 'home' } },
      { path: 'assessment', name: 'assessment', component: () => import('../views/user/AssessmentView.vue'), meta: { requiresAuth: true, guestRedirect: 'home' } },
      { path: 'login', name: 'login', component: () => import('../views/user/LoginView.vue') },
      { path: 'register', name: 'register', component: () => import('../views/user/RegisterView.vue') },
      { path: 'profile', name: 'profile', component: () => import('../views/user/ProfileView.vue'), meta: { requiresAuth: true } },
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
  const auth = useAuthStore()
  if (to.meta.requiresAdmin) {
    if (!auth.isAdmin) return { name: 'admin-login' }
  }
  if (to.meta.requiresAuth && !auth.isLoggedIn) {
    return { name: to.meta.guestRedirect || 'login' }
  }
  return true
})

export default router
