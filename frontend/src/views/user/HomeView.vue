<template>
  <div>
    <!-- 主视觉 -->
    <section class="hero mhop-card">
      <div class="hero-text">
        <div class="hero-badge">
          <el-icon><MagicStick /></el-icon> 全国性 · 匿名优先 · 免费公益
        </div>
        <h1>在这里，你不必独自承受</h1>
        <p class="hero-sub">
          当情绪有重量，说出来就是改变的开始。AI 心理助手 7×24 小时即时回应你，
          社区里有相似经历的人彼此陪伴，专业援助热线始终为你待命。
        </p>
        <div class="hero-actions">
          <template v-if="auth.isLoggedIn">
            <router-link to="/forum"><el-button type="primary" size="large" round><el-icon><EditPen /></el-icon>我想倾诉</el-button></router-link>
            <router-link to="/assessment"><el-button size="large" round plain><el-icon><DataAnalysis /></el-icon>做一次心理评估</el-button></router-link>
          </template>
          <template v-else>
            <router-link to="/register"><el-button type="primary" size="large" round><el-icon><EditPen /></el-icon>注册</el-button></router-link>
            <router-link to="/login"><el-button size="large" round plain><el-icon><User /></el-icon>登录</el-button></router-link>
          </template>
        </div>
        <div class="hero-tips">
          <span><el-icon><Lock /></el-icon> 匿名发布，保护隐私</span>
          <span><el-icon><ChatDotRound /></el-icon> AI 秒级回应</span>
          <span><el-icon><PhoneFilled /></el-icon> 危机一键转热线</span>
        </div>
      </div>
      <div class="hero-illu">
        <el-icon :size="150"><Sunny /></el-icon>
      </div>
    </section>

    <!-- 服务入口（登录后可见） -->
    <section v-if="auth.isLoggedIn" class="feature-grid">
      <router-link to="/forum" class="feature-card mhop-card">
        <span class="feature-icon" style="background: #e7f3f1; color: #2f8f83"><el-icon :size="26"><ChatLineSquare /></el-icon></span>
        <h3>互助论坛</h3>
        <p class="text-sub">匿名说出困扰，AI 即时回应，社区温暖陪伴，管理员持续巡检。</p>
      </router-link>
      <router-link to="/assessment" class="feature-card mhop-card">
        <span class="feature-icon" style="background: #fdf3e7; color: #d98a2b"><el-icon :size="26"><DocumentChecked /></el-icon></span>
        <h3>AI 心理评估</h3>
        <p class="text-sub">PHQ-9 / GAD-7 标准量表 + 自由倾诉，生成个性化解读与求助指引。</p>
      </router-link>
      <div class="feature-card mhop-card" @click="goHotline">
        <span class="feature-icon" style="background: #fdecea; color: #d83a2e"><el-icon :size="26"><PhoneFilled /></el-icon></span>
        <h3>紧急援助</h3>
        <p class="text-sub">全国心理援助热线 12356、危机干预 010-82951332，生命优先，时刻在线。</p>
      </div>
    </section>

    <!-- 最新倾诉（登录后可见） -->
    <section v-if="auth.isLoggedIn" style="margin-top: 26px">
      <div class="section-head">
        <h2><el-icon><ChatDotSquare /></el-icon> 最新倾诉</h2>
        <router-link to="/forum" class="text-sub">进入论坛 →</router-link>
      </div>
      <div v-loading="loading">
        <div v-for="p in posts" :key="p.id" class="post-item mhop-card" @click="$router.push(`/forum/${p.id}`)">
          <div class="post-meta">
            <span class="board-chip" :style="chipStyle(p.board)">{{ boardOf(p.board).name }}</span>
            <el-tag size="small" :type="p.is_anonymous ? 'info' : 'success'" effect="plain">
              {{ p.is_anonymous ? '匿名朋友' : p.author }}
            </el-tag>
            <el-tag v-if="p.crisis" size="small" type="danger" effect="light">危机关注</el-tag>
            <el-tag v-if="p.ai_replied" size="small" type="success" effect="light">AI 已回应</el-tag>
            <span class="text-sub time-tag" style="margin-left: auto; font-size: 12.5px">{{ fromNow(p.created_at) }}</span>
          </div>
          <p class="post-content">{{ p.content }}</p>
          <div class="text-sub" style="font-size: 13px">
            <el-icon><ChatLineRound /></el-icon> {{ p.reply_count }} 条回应
          </div>
        </div>
        <el-empty v-if="!loading && posts.length === 0" description="还没有帖子，来发出第一份倾诉吧" />
      </div>
    </section>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import http from '../../api'
import { fromNow } from '../../utils/format'
import { boardOf } from '../../utils/boards'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const posts = ref([])
const loading = ref(false)

function chipStyle(slug) {
  const b = boardOf(slug || 'mood')
  return {
    background: `${b.color}1a`,
    color: b.color,
    border: `1px solid ${b.color}55`,
  }
}

onMounted(async () => {
  if (!auth.isLoggedIn) return
  loading.value = true
  try {
    const data = await http.get('/forum/posts', { params: { page: 1, size: 5 } })
    posts.value = data.items
  } finally {
    loading.value = false
  }
})

function goHotline() {
  ElMessage.success('请查看页面顶部红色紧急援助横幅，或直接拨打 12356')
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
</script>

<style scoped>
.hero {
  margin-top: 22px;
  padding: 44px 48px;
  background: linear-gradient(120deg, #ffffff 0%, #eef7f5 100%);
  display: flex;
  align-items: center;
  gap: 30px;
  overflow: hidden;
}
.hero-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #e7f3f1;
  color: #227067;
  font-size: 13px;
  padding: 5px 14px;
  border-radius: 999px;
  margin-bottom: 16px;
}
.hero h1 {
  font-size: 34px;
  margin: 0 0 14px;
  letter-spacing: 1px;
}
.hero-sub {
  color: var(--mhop-text-sub);
  font-size: 15.5px;
  line-height: 1.9;
  max-width: 620px;
}
.hero-actions {
  display: flex;
  gap: 14px;
  margin: 24px 0 18px;
}
.hero-actions .el-icon {
  margin-right: 4px;
}
.hero-tips {
  display: flex;
  gap: 22px;
  color: var(--mhop-text-sub);
  font-size: 13.5px;
}
.hero-tips span {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}
.hero-illu {
  margin-left: auto;
  color: #c9e4e0;
  animation: floaty 4s ease-in-out infinite;
}
@keyframes floaty {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}
.feature-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 18px;
  margin-top: 22px;
}
.feature-card {
  display: block;
  padding: 24px;
  cursor: pointer;
  transition: transform 0.15s, box-shadow 0.15s;
}
.feature-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 22px rgba(38, 58, 55, 0.1);
}
.feature-icon {
  width: 52px;
  height: 52px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 12px;
}
.feature-card h3 {
  margin: 0 0 8px;
  font-size: 17px;
}
.feature-card p {
  margin: 0;
  font-size: 13.5px;
  line-height: 1.75;
}
.section-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 0 0 14px;
}
.section-head h2 {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 19px;
  margin: 0;
}
.post-item {
  padding: 16px 20px;
  margin-bottom: 12px;
  cursor: pointer;
  transition: box-shadow 0.15s;
}
.post-item:hover {
  box-shadow: 0 6px 18px rgba(38, 58, 55, 0.1);
}
.post-meta {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}
.post-meta .time-tag {
  margin-left: auto;
}
.board-chip {
  font-size: 12px;
  padding: 1px 9px;
  border-radius: 999px;
  font-weight: 600;
}
.post-content {
  margin: 10px 0;
  line-height: 1.75;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
@media (max-width: 760px) {
  .hero {
    flex-direction: column-reverse;
    padding: 26px 20px;
  }
  .hero-illu {
    margin: 0;
  }
  .hero-illu .el-icon {
    font-size: 96px !important;
  }
  .hero h1 {
    font-size: 25px;
    letter-spacing: 0;
  }
  .hero-sub {
    font-size: 14px;
  }
  .hero-actions {
    flex-wrap: wrap;
  }
  .hero-actions .el-button {
    margin-left: 0 !important;
  }
  .feature-grid {
    grid-template-columns: 1fr;
    gap: 12px;
  }
  .hero-tips {
    flex-wrap: wrap;
    gap: 10px;
  }
  .post-item {
    padding: 14px 15px;
  }
  .post-meta .time-tag {
    margin-left: 0;
  }
  .feature-card {
    padding: 20px;
  }
}
</style>
