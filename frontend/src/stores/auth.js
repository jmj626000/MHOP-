import { defineStore } from 'pinia'
import http from '../api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('mhop_token') || '',
    user: JSON.parse(localStorage.getItem('mhop_user') || 'null'),
  }),
  getters: {
    isLoggedIn: (s) => !!s.token,
    isAdmin: (s) => s.user?.role === 'admin',
    displayName: (s) => s.user?.username || '',
  },
  actions: {
    setAuth(token, user) {
      this.token = token
      this.user = user
      localStorage.setItem('mhop_token', token)
      localStorage.setItem('mhop_user', JSON.stringify(user))
    },
    async login(username, password) {
      const data = await http.post('/auth/login', { username, password })
      this.setAuth(data.access_token, data.user)
      return data.user
    },
    async register(username, password) {
      const data = await http.post('/auth/register', { username, password })
      this.setAuth(data.access_token, data.user)
      return data.user
    },
    async restore() {
      if (!this.token) return
      try {
        this.user = await http.get('/auth/me')
        localStorage.setItem('mhop_user', JSON.stringify(this.user))
      } catch {
        this.logout()
      }
    },
    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('mhop_token')
      localStorage.removeItem('mhop_user')
    },
  },
})
