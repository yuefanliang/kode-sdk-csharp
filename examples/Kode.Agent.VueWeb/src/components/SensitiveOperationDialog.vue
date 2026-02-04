<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    width="500px"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    @close="handleCancel"
  >
    <div class="sensitive-approval-dialog">
      <div class="warning-icon">
        <el-icon :size="48" color="#e6a23c">
          <Warning />
        </el-icon>
      </div>

      <div class="operation-info">
        <h3>{{ operationTitle }}</h3>
        <p class="warning-text">{{ warningMessage }}</p>

        <div v-if="toolName" class="tool-details">
          <div class="detail-item">
            <span class="label">工具名称：</span>
            <span class="value">{{ toolName }}</span>
          </div>

          <div v-if="hasArguments" class="detail-item">
            <span class="label">操作参数：</span>
            <el-collapse>
              <el-collapse-item title="查看参数">
                <pre class="args-content">{{ formatArguments(toolArguments) }}</pre>
              </el-collapse-item>
            </el-collapse>
          </div>

          <div v-if="sessionId" class="detail-item">
            <span class="label">会话ID：</span>
            <span class="value">{{ sessionId }}</span>
          </div>

          <div class="detail-item">
            <span class="label">创建时间：</span>
            <span class="value">{{ formatTime(createdAt) }}</span>
          </div>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button @click="handleCancel" :disabled="loading">
          取消操作
        </el-button>
        <el-button
          type="danger"
          @click="handleConfirm"
          :loading="loading"
        >
          我已了解风险，继续执行
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Warning } from '@element-plus/icons-vue'
import type { Approval } from '@/types'

interface Props {
  modelValue: boolean
  approval: Approval | null
}

const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: [approval: Approval, note?: string]
  cancel: []
}>()

const loading = ref(false)
const note = ref('')

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const toolName = computed(() => props.approval?.toolName || '')
const operationType = computed(() => props.approval?.operationType || 0)
const toolArguments = computed(() => props.approval?.arguments)
const sessionId = computed(() => props.approval?.sessionId || props.approval?.agentId || '')
const createdAt = computed(() => props.approval?.createdAt || '')

const hasArguments = computed(() => {
  const args = toolArguments.value
  return args && !(typeof args === 'string' && args.trim() === '')
})

const operationTitle = computed(() => {
  if (!props.approval) return ''
  const type = operationType.value
  return type === 3 ? '敏感操作警告：删除文件'
           : type === 4 ? '敏感操作警告：执行命令'
           : type === 2 ? '操作确认：写入文件'
           : '操作确认'
})

const warningMessage = computed(() => {
  if (!props.approval) return ''
  const type = operationType.value
  return type === 3
    ? '此操作将永久删除文件或目录，删除后无法恢复。请确认是否继续执行此操作？'
    : type === 4
    ? '此操作将执行系统命令，可能对系统造成影响。请确认是否继续执行此操作？'
    : type === 2
    ? '此操作将修改或创建文件。请确认是否继续执行此操作？'
    : '请确认是否继续执行此操作？'
})

const dialogTitle = computed(() => {
  return props.approval?.isSensitive ? '⚠️ 敏感操作确认' : '操作确认'
})

function formatArguments(args: any) {
  if (!args) return ''
  if (typeof args === 'string') return args
  try {
    return JSON.stringify(args, null, 2)
  } catch {
    return String(args)
  }
}

function formatTime(time: string) {
  const date = parseUtc8Date(time)
  if (!date) return ''
  const display = date.toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    timeZone: 'Asia/Shanghai'
  })
  return `${display} UTC+8`
}

function parseUtc8Date(time: string) {
  const trimmed = time.trim()
  const hasZone = /z$/i.test(trimmed) || /[+-]\d{2}:?\d{2}$/.test(trimmed)
  const normalized = hasZone
    ? trimmed
    : trimmed.includes(' ') && !trimmed.includes('T')
      ? `${trimmed.replace(' ', 'T')}Z`
      : `${trimmed}Z`
  const date = new Date(normalized)
  if (Number.isNaN(date.getTime())) return null
  return date
}

async function handleConfirm() {
  if (!props.approval) return

  loading.value = true
  try {
    emit('confirm', props.approval, note.value || undefined)
    visible.value = false
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('cancel')
  visible.value = false
}
</script>

<style scoped>
.sensitive-approval-dialog {
  padding: 20px 0;
}

.warning-icon {
  text-align: center;
  margin-bottom: 20px;
}

.operation-info h3 {
  text-align: center;
  margin: 0 0 16px 0;
  font-size: 18px;
  font-weight: 600;
  color: #e6a23c;
}

.warning-text {
  text-align: center;
  color: #606266;
  font-size: 14px;
  line-height: 1.6;
  margin: 0 0 24px 0;
  padding: 12px;
  background: #fef0f0;
  border-radius: 4px;
  border-left: 3px solid #e6a23c;
}

.tool-details {
  background: #f5f7fa;
  padding: 16px;
  border-radius: 4px;
  border: 1px solid #e4e7ed;
}

.detail-item {
  display: flex;
  margin-bottom: 12px;
  font-size: 13px;
}

.detail-item:last-child {
  margin-bottom: 0;
}

.detail-item .label {
  color: #909399;
  min-width: 80px;
  flex-shrink: 0;
}

.detail-item .value {
  color: #303133;
  word-break: break-all;
  flex: 1;
}

.args-content {
  font-size: 12px;
  background: #fff;
  padding: 8px;
  border-radius: 4px;
  white-space: pre-wrap;
  word-break: break-all;
  color: #606266;
  margin: 8px 0 0 80px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
