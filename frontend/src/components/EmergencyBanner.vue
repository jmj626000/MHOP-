<template>
  <div class="emergency-banner">
    <div class="emergency-bar" @click="expanded = !expanded">
      <span class="emergency-dot"></span>
      <el-icon><PhoneFilled /></el-icon>
      <span class="emergency-title">紧急心理援助 · 24小时全国热线</span>
      <span class="emergency-main-phone">12356</span>
      <span class="emergency-toggle">
        {{ expanded ? '收起' : '查看全部热线' }}
        <el-icon style="vertical-align: -2px"><component :is="expanded ? 'ArrowUp' : 'ArrowDown'" /></el-icon>
      </span>
    </div>
    <div v-if="expanded" class="emergency-detail">
      <a v-for="h in hotlines" :key="h.name" class="hotline-card" :href="'tel:' + h.phone.replace(/[^0-9]/g, '')">
        <div class="tag">{{ h.tag }}</div>
        <div class="phone">{{ h.phone || '号码配置中' }}</div>
        <div style="font-weight: 700; font-size: 14px">{{ h.name }}</div>
        <div class="desc">{{ h.description }}</div>
      </a>
      <div class="hotline-card" style="cursor: default">
        <div class="tag">如果你身边有人处于危机中</div>
        <div style="font-size: 13.5px; line-height: 1.7; margin-top: 6px">
          请陪伴在 TA 身边、移除身边的危险物品，鼓励 TA 拨打热线，必要时立即送医或拨打 110/120。
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import http from '../api'

const expanded = ref(false)
const hotlines = ref([])

onMounted(async () => {
  try {
    hotlines.value = await http.get('/hotlines')
  } catch {
    /* 用默认值兜底 */
    hotlines.value = [
      { name: '全国心理援助热线', phone: '12356', tag: '全国通用 · 24小时', description: '免费、保密' },
    ]
  }
})
</script>
