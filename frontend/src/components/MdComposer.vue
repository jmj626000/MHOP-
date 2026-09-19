<template>
  <div class="md-composer">
    <div class="md-composer-bar">
      <el-radio-group v-model="mode" size="small">
        <el-radio-button value="edit">写作</el-radio-button>
        <el-radio-button value="preview">预览</el-radio-button>
      </el-radio-group>
      <span class="text-sub md-hint">支持 Markdown：**加粗** · - 列表 · # 标题 · [链接](https://)</span>
    </div>
    <el-input
      v-if="mode === 'edit'"
      :model-value="modelValue"
      @update:model-value="$emit('update:modelValue', $event)"
      type="textarea"
      :rows="rows"
      :maxlength="maxlength"
      :show-word-limit="showWordLimit"
      resize="none"
      :placeholder="placeholder"
    />
    <div v-else class="md-body md-preview" v-html="rendered"></div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { renderMarkdown } from '../utils/markdown'

const props = defineProps({
  modelValue: { type: String, default: '' },
  rows: { type: Number, default: 4 },
  placeholder: { type: String, default: '' },
  maxlength: { type: Number, default: 2000 },
  showWordLimit: { type: Boolean, default: true },
})
defineEmits(['update:modelValue'])

const mode = ref('edit')
const rendered = computed(() => renderMarkdown(props.modelValue) || '<span class="text-sub">还没有内容</span>')
</script>

<style scoped>
.md-composer-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}
.md-hint {
  font-size: 12px;
}
.md-preview {
  min-height: 110px;
  padding: 12px 14px;
  border: 1px solid var(--el-border-color);
  border-radius: 10px;
  background: #fafaf8;
}
@media (max-width: 640px) {
  .md-hint {
    display: none;
  }
}
</style>
