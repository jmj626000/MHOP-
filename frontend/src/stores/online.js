import { defineStore } from 'pinia'
import http from '../api'

function anonId() {
  let id = localStorage.getItem('mhop_anon_id')
  if (!id) {
    id = 'anon-' + Math.random().toString(36).slice(2) + Date.now().toString(36)
    localStorage.setItem('mhop_anon_id', id)
  }
  return id
}

export const useOnlineStore = defineStore('online', {
  state: () => ({ count: 0, timer: null }),
  actions: {
    async beat() {
      try {
        const data = await http.post('/online/heartbeat', { key: anonId() }, { silent: true })
        this.count = data.online
      } catch {
        /* 心跳失败静默 */
      }
    },
    start() {
      this.beat()
      this.timer = setInterval(this.beat, 30000)
    },
  },
})
