<template>
  <router-view />
</template>

<script setup>
import { onMounted } from 'vue'
import { useAuthStore } from './stores/auth'
import { useOnlineStore } from './stores/online'

const auth = useAuthStore()
const online = useOnlineStore()

onMounted(() => {
  auth.restore()
  online.start()
})
</script>
