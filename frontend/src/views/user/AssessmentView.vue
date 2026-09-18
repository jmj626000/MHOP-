<template>
  <div>
    <div class="page-head">
      <h2><el-icon><DataAnalysis /></el-icon> AI 心理评估</h2>
      <p class="text-sub">
        采用国际通用量表 + AI 个性化解读，结果仅供自我了解，不构成医学诊断。
        <strong style="color: var(--mhop-text)">匿名状态下结果只保存在你的浏览器本地</strong>，登录后可选择加密上传云端。
      </p>
    </div>

    <!-- 类型选择 -->
    <el-radio-group v-model="type" class="type-group" @change="onTypeChange">
      <el-radio-button v-for="s in scales" :key="s.key" :value="s.key">
        {{ s.key === 'phq9' ? 'PHQ-9 抑郁筛查' : s.key === 'gad7' ? 'GAD-7 焦虑筛查' : '自由倾诉评估' }}
      </el-radio-button>
    </el-radio-group>

    <div class="assess-layout">
      <!-- 作答区 -->
      <section class="mhop-card form-card">
        <!-- 量表 -->
        <template v-if="type !== 'free' && currentScale">
          <el-alert :title="currentScale.name" :description="currentScale.intro" type="info" :closable="false" show-icon
            style="margin-bottom: 18px" />
          <div v-for="(q, i) in currentScale.questions" :key="i" class="question-item">
            <div class="q-title"><span class="q-no">{{ i + 1 }}</span>{{ q }}</div>
            <el-radio-group v-model="answers[i]">
              <el-radio v-for="o in currentScale.options" :key="o.value" :value="o.value" border size="small">
                {{ o.label }}
              </el-radio>
            </el-radio-group>
          </div>
          <el-divider />
          <div class="free-block">
            <label>补充描述（可选，帮助 AI 更好地理解你）</label>
            <el-input v-model="freeText" type="textarea" :rows="3" maxlength="1000" show-word-limit resize="none"
              placeholder="比如：这种状态持续多久了、有没有明显诱因、睡眠和饮食怎么样……" />
          </div>
        </template>

        <!-- 自由倾诉 -->
        <template v-else>
          <el-alert title="用自己的话描述最近的状态" type="success" :closable="false" show-icon
            description="想到什么写什么，没有对错。AI 会从情绪状态、可能的压力来源、自助建议和求助路径几个方面回应你。"
            style="margin-bottom: 16px" />
          <el-input v-model="freeText" type="textarea" :rows="10" maxlength="3000" show-word-limit resize="none"
            placeholder="最近发生了什么？你的情绪、睡眠、身体感受是怎样的？什么时刻最难熬？……" />
        </template>

        <el-alert v-if="crisisHint" type="error" :closable="false" show-icon style="margin-top: 14px"
          title="你描述的内容里出现了伤害自己的信号"
          description="请立即拨打 12356（24小时）或 010-82951332；若有立即危险请拨打 110/120，不要独处。" />

        <div class="submit-row">
          <el-checkbox v-if="auth.isLoggedIn" v-model="saveCloud">加密保存到云端（可跨设备查看）</el-checkbox>
          <span v-else class="text-sub" style="font-size: 12.5px">未登录：结果仅保存在本浏览器</span>
          <el-button type="primary" size="large" round :loading="loading" @click="submit">
            <el-icon><MagicStick /></el-icon> 生成 AI 评估
          </el-button>
        </div>
      </section>

      <!-- 结果区 -->
      <aside>
        <section v-if="result" class="mhop-card result-card">
          <el-alert v-if="result.crisis" type="error" :closable="false" show-icon style="margin-bottom: 14px"
            title="检测到危机信号，请优先保证自己的安全"
            description="立即拨打 12356 / 010-82951332，紧急时拨打 110/120，并联系身边信任的人陪伴。" />
          <div v-if="result.score !== null" class="score-box">
            <div>
              <div class="score-num" :class="result.level_code">{{ result.score }}</div>
              <div class="text-sub" style="font-size: 12px">量表得分</div>
            </div>
            <el-tag :type="levelType(result.level_code)" size="large" effect="light">{{ result.level }}</el-tag>
          </div>
          <el-divider style="margin: 14px 0" />
          <p class="pre-wrap ai-text">{{ result.ai_result }}</p>
          <el-tag v-if="result.saved_cloud" type="success" size="small" effect="plain" style="margin-top: 10px">
            <el-icon><Cloudy /></el-icon> 已保存到云端
          </el-tag>
          <el-tag v-else type="info" size="small" effect="plain" style="margin-top: 10px">
            <el-icon><Files /></el-icon> 已保存在本浏览器
          </el-tag>
        </section>

        <section class="mhop-card history-card">
          <h4><el-icon><Clock /></el-icon> 我的评估记录</h4>
          <el-tabs v-model="historyTab">
            <el-tab-pane :label="`本地记录 (${localHistory.length})`" name="local">
              <div v-if="localHistory.length === 0" class="text-sub" style="font-size: 13px">暂无本地记录</div>
              <div v-for="h in localHistory" :key="h.id" class="history-item" @click="showDetail(h)">
                <span>{{ typeName(h.assessment_type) }}</span>
                <el-tag size="small" :type="levelType(h.level_code)" effect="plain">
                  {{ h.score !== null ? `${h.score}分 · ` : '' }}{{ h.level || 'AI 解读' }}
                </el-tag>
                <span class="text-sub" style="font-size: 12px; margin-left: auto">{{ fmtTime(h.created_at) }}</span>
              </div>
            </el-tab-pane>
            <el-tab-pane v-if="auth.isLoggedIn" :label="`云端记录 (${cloudHistory.length})`" name="cloud">
              <div v-if="cloudHistory.length === 0" class="text-sub" style="font-size: 13px">暂无云端记录</div>
              <div v-for="h in cloudHistory" :key="h.id" class="history-item" @click="showDetail(h)">
                <span>{{ typeName(h.assessment_type) }}</span>
                <el-tag size="small" :type="levelType(h.level_code)" effect="plain">
                  {{ h.score !== null ? `${h.score}分 · ` : '' }}{{ h.level || 'AI 解读' }}
                </el-tag>
                <span class="text-sub" style="font-size: 12px; margin-left: auto">{{ fmtTime(h.created_at) }}</span>
              </div>
            </el-tab-pane>
          </el-tabs>
        </section>
      </aside>
    </div>

    <el-dialog v-model="detailVisible" title="评估详情" width="620px">
      <div v-if="detail">
        <div style="margin-bottom: 10px">
          <el-tag :type="levelType(detail.level_code)" effect="light">
            {{ typeName(detail.assessment_type) }} · {{ detail.score !== null ? `${detail.score} 分 · ` : '' }}{{ detail.level || 'AI 解读' }}
          </el-tag>
          <span class="text-sub" style="margin-left: 10px; font-size: 12.5px">{{ fmtTime(detail.created_at) }}</span>
        </div>
        <p class="pre-wrap">{{ detail.ai_result }}</p>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import http from '../../api'
import { useAuthStore } from '../../stores/auth'
import { hasCrisisHint } from '../../utils/crisis'
import { fmtTime } from '../../utils/format'

const auth = useAuthStore()
const scales = ref([])
const type = ref('phq9')
const answers = ref({})
const freeText = ref('')
const saveCloud = ref(false)
const loading = ref(false)
const result = ref(null)
const historyTab = ref('local')
const localHistory = ref([])
const cloudHistory = ref([])
const detail = ref(null)
const detailVisible = ref(false)

const currentScale = computed(() => scales.value.find((s) => s.key === type.value))
const crisisHint = computed(() => hasCrisisHint(freeText.value) || (type.value === 'phq9' && (answers.value[8] ?? 0) > 0))

function levelType(code) {
  return { normal: 'info', mild: 'success', moderate: 'warning', severe: 'danger', danger: 'danger' }[code] || 'info'
}
function typeName(key) {
  return { phq9: 'PHQ-9 抑郁筛查', gad7: 'GAD-7 焦虑筛查', free: '自由倾诉评估' }[key] || key
}
function onTypeChange() {
  answers.value = {}
  freeText.value = ''
  result.value = null
}

async function submit() {
  if (type.value !== 'free') {
    const n = currentScale.value.questions.length
    for (let i = 0; i < n; i++) {
      if (answers.value[i] === undefined) {
        ElMessage.warning(`请完成第 ${i + 1} 题`)
        return
      }
    }
  } else if (!freeText.value.trim()) {
    ElMessage.warning('请先描述你最近的状态')
    return
  }
  loading.value = true
  try {
    result.value = await http.post('/assessments', {
      assessment_type: type.value,
      answers: answers.value,
      free_text: freeText.value.trim(),
      save_to_cloud: saveCloud.value,
    })
    if (!result.value.saved_cloud) saveLocal(result.value)
    if (saveCloud.value) await loadCloud()
    ElMessage.success('评估完成')
  } finally {
    loading.value = false
  }
}

function saveLocal(r) {
  const list = JSON.parse(localStorage.getItem('mhop_local_assessments') || '[]')
  list.unshift({
    id: 'local-' + Date.now(),
    assessment_type: r.assessment_type,
    score: r.score,
    level: r.level,
    level_code: r.level_code,
    ai_result: r.ai_result,
    created_at: new Date().toISOString(),
  })
  localHistory.value = list.slice(0, 20)
  localStorage.setItem('mhop_local_assessments', JSON.stringify(localHistory.value))
}

async function loadCloud() {
  if (!auth.isLoggedIn) {
    cloudHistory.value = []
    return
  }
  cloudHistory.value = await http.get('/assessments/mine')
}

function showDetail(h) {
  detail.value = h
  detailVisible.value = true
}

onMounted(async () => {
  scales.value = await http.get('/assessments/scales')
  localHistory.value = JSON.parse(localStorage.getItem('mhop_local_assessments') || '[]')
  loadCloud()
})
</script>

<style scoped>
.page-head h2 {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 4px 0 8px;
}
.page-head p {
  font-size: 13.5px;
  line-height: 1.8;
  max-width: 760px;
}
.type-group {
  margin: 16px 0;
  flex-wrap: wrap;
  gap: 8px;
}
.assess-layout {
  display: grid;
  grid-template-columns: 1fr 340px;
  gap: 18px;
  align-items: start;
}
.form-card {
  padding: 24px;
}
.question-item {
  margin-bottom: 18px;
}
.q-title {
  margin-bottom: 8px;
  font-size: 14.5px;
  line-height: 1.6;
}
.q-no {
  display: inline-flex;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--mhop-teal-light);
  color: var(--mhop-teal-dark);
  font-size: 12.5px;
  align-items: center;
  justify-content: center;
  margin-right: 8px;
  font-weight: 700;
}
.free-block label {
  display: block;
  font-size: 13.5px;
  margin-bottom: 8px;
  color: var(--mhop-text-sub);
}
.submit-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 18px;
  gap: 12px;
}
.result-card {
  padding: 20px;
  position: sticky;
  top: 90px;
}
.score-box {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.score-num {
  font-size: 40px;
  font-weight: 800;
  line-height: 1;
  color: var(--mhop-teal);
}
.score-num.moderate {
  color: #d98a2b;
}
.score-num.severe,
.score-num.danger {
  color: var(--mhop-danger);
}
.ai-text {
  font-size: 14px;
  max-height: 420px;
  overflow-y: auto;
}
.history-card {
  margin-top: 16px;
  padding: 16px 18px 8px;
  position: sticky;
  top: 400px;
}
.history-card h4 {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0 0 6px;
  font-size: 15px;
}
.history-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 4px;
  border-bottom: 1px dashed #ece7dd;
  font-size: 13px;
  cursor: pointer;
}
.history-item:hover {
  background: #faf8f3;
}
@media (max-width: 900px) {
  .assess-layout {
    grid-template-columns: 1fr;
  }
  .result-card,
  .history-card {
    position: static;
  }
}
</style>
