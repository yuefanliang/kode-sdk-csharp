<template>
  <div class="approval-panel">
    <div class="approval-header">
      <span>待审批事项</span>
      <el-badge :value="pendingCount" :hidden="pendingCount === 0" type="danger" />
    </div>

    <div v-if="loading" class="loading-container">
      <el-icon class="is-loading"><Loading /></el-icon>
    </div>

    <div v-else class="approval-items">
      <div
        v-for="approval in pendingApprovals"
        :key="approval.approvalId"
        class="approval-item"
      >
        <div class="approval-info">
          <div class="approval-tool">
            <el-tag size="small">{{ approval.toolName }}</el-tag>
          </div>
          <div class="approval-meta">
            <span class="meta-label">会话</span>
            <span class="meta-value">{{ approval.sessionId || approval.agentId || '-' }}</span>
          </div>
          <div class="approval-meta">
            <span class="meta-label">用户</span>
            <span class="meta-value">{{ approval.userId }}</span>
          </div>
          <div class="approval-description">{{ approval.description }}</div>
          <div class="approval-arguments" v-if="approval.arguments">
            <el-collapse>
              <el-collapse-item title="查看参数" name="args">
                <pre class="args-content">{{ formatArguments(approval.arguments) }}</pre>
              </el-collapse-item>
            </el-collapse>
          </div>
          <div class="approval-context" v-if="hasContext(approval)">
            <el-collapse>
              <el-collapse-item title="查看上下文" name="context">
                <pre class="context-content">{{ formatContext(approval) }}</pre>
              </el-collapse-item>
            </el-collapse>
          </div>
          <div class="approval-time">{{ formatTime(approval.createdAt) }}</div>
        </div>

        <div class="approval-actions">
          <el-button
            type="success"
            size="small"
            @click="handleConfirm(approval)"
            :loading="loadingAction === approval.approvalId"
            :disabled="loadingAction !== null"
          >
            <el-icon><Select /></el-icon>
            同意
          </el-button>
          <el-button
            type="danger"
            size="small"
            @click="handleCancel(approval)"
            :loading="loadingAction === approval.approvalId"
            :disabled="loadingAction !== null"
          >
            <el-icon><Close /></el-icon>
            拒绝
          </el-button>
          <el-tag v-if="loadingAction === approval.approvalId" size="small" type="warning">
            处理中
          </el-tag>
        </div>
      </div>

      <el-empty
        v-if="pendingApprovals.length === 0"
        description="暂无待审批事项"
        :image-size="60"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Loading, Select, Close } from '@element-plus/icons-vue'
import { useApprovalStore } from '@/stores/approval'
import { useUserStore } from '@/stores/user'
import type { Approval } from '@/types'

const approvalStore = useApprovalStore()
const userStore = useUserStore()

const loading = ref(false)
const loadingAction = ref<string | null>(null)

const pendingApprovals = computed(() => approvalStore.pendingApprovals)
const pendingCount = computed(() => pendingApprovals.value.length)

async function loadApprovals() {
  loading.value = true
  try {
    await approvalStore.loadPendingApprovals(userStore.DEFAULT_USER_ID)
  } catch (error) {
    console.error('加载待审批列表失败:', error)
  } finally {
    loading.value = false
  }
}

async function handleConfirm(approval: Approval) {
  try {
    const value = await ElMessageBox.prompt('请输入备注（可选）', '同意审批', {
      confirmButtonText: '同意',
      cancelButtonText: '取消',
      inputPattern: /.*/,
      inputErrorMessage: '备注不能为空'
    })

    loadingAction.value = approval.approvalId
    await approvalStore.confirmApproval(
      approval.approvalId,
      userStore.DEFAULT_USER_ID,
      value.value || undefined
    )

    ElMessage.success('已同意审批')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '同意失败')
    }
  } finally {
    loadingAction.value = null
  }
}

async function handleCancel(approval: Approval) {
  try {
    const value = await ElMessageBox.prompt('请输入备注（可选）', '拒绝审批', {
      confirmButtonText: '拒绝',
      cancelButtonText: '返回',
      inputPattern: /.*/,
      inputErrorMessage: '备注不能为空'
    })

    loadingAction.value = approval.approvalId
    await approvalStore.cancelApproval(
      approval.approvalId,
      userStore.DEFAULT_USER_ID,
      value.value || undefined
    )

    ElMessage.success('已拒绝审批')
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.message || '拒绝失败')
    }
  } finally {
    loadingAction.value = null
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

function formatArguments(args: Approval['arguments']) {
  if (typeof args === 'string') return args
  try {
    return JSON.stringify(args, null, 2)
  } catch {
    return String(args)
  }
}

function hasContext(approval: Approval) {
  return !!(approval.arguments || approval.description || approval.sessionId || approval.agentId)
}

function formatContext(approval: Approval) {
  const payload = {
    toolName: approval.toolName,
    description: approval.description,
    sessionId: approval.sessionId,
    agentId: approval.agentId,
    userId: approval.userId,
    arguments: approval.arguments,
    createdAt: approval.createdAt
  }
  return JSON.stringify(payload, null, 2)
}

onMounted(() => {
  loadApprovals()
  // 每隔30秒刷新一次
  setInterval(loadApprovals, 30000)
})
</script>

<style scoped>
.approval-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.approval-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  border-bottom: 1px solid #e4e7ed;
}

.loading-container {
  display: flex;
  justify-content: center;
  padding: 20px;
}

.approval-items {
  flex: 1;
  overflow-y: auto;
  padding: 10px;
}

.approval-item {
  padding: 12px;
  margin-bottom: 12px;
  background: #fff;
  border-radius: 6px;
  border: 1px solid #e4e7ed;
}

.approval-tool {
  margin-bottom: 8px;
}

.approval-description {
  font-size: 13px;
  color: #303133;
  margin-bottom: 8px;
  line-height: 1.5;
}

.approval-meta {
  display: flex;
  gap: 6px;
  font-size: 12px;
  color: #606266;
  margin-bottom: 6px;
  align-items: center;
}

.meta-label {
  color: #909399;
}

.meta-value {
  word-break: break-all;
}

.approval-arguments {
  margin-bottom: 8px;
}

.args-content {
  font-size: 12px;
  background: #f5f7fa;
  padding: 8px;
  border-radius: 4px;
  white-space: pre-wrap;
  word-break: break-all;
  color: #606266;
}

.approval-context {
  margin-bottom: 8px;
}

.context-content {
  font-size: 12px;
  background: #f5f7fa;
  padding: 8px;
  border-radius: 4px;
  white-space: pre-wrap;
  word-break: break-all;
  color: #606266;
}

.approval-time {
  font-size: 12px;
  color: #909399;
  margin-bottom: 8px;
}

.approval-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}
</style>
