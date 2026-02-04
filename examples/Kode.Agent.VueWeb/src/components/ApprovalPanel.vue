<template>
  <div class="approval-panel">
    <div class="panel-header">
      <div class="panel-title">
        <el-icon><FolderOpened /></el-icon>
        <span>工作区</span>
      </div>
    </div>

    <div class="panel-content">
      <!-- Skill 管理 -->
      <SessionSkillManager
        v-if="sessionId"
        :session-id="sessionId"
        class="skill-manager-section"
      />

      <!-- 工作区浏览器 -->
      <div class="workspace-section">
        <div class="section-title">工作区文件</div>
        <div v-if="workspaceLoading" class="loading-container">
          <el-icon class="is-loading"><Loading /></el-icon>
          <span>加载工作区中...</span>
        </div>

        <div v-else-if="!currentWorkspacePath" class="workspace-empty">
          <el-icon><Warning /></el-icon>
          <span>当前会话未配置工作区</span>
        </div>

        <WorkspaceBrowser
          v-else
          :workspace-path="currentWorkspacePath"
          @file-select="handleFileSelect"
        />
      </div>
    </div>

    <!-- 文件预览对话框 -->
    <el-dialog
      v-model="previewVisible"
      :title="previewFileName"
      width="80%"
      :fullscreen="isFullscreen"
      class="preview-dialog"
      destroy-on-close
    >
      <template #header>
        <div class="preview-dialog-header">
          <span>{{ previewFileName }}</span>
          <el-button
            text
            :icon="isFullscreen ? Crop : FullScreen"
            @click="isFullscreen = !isFullscreen"
          >
            {{ isFullscreen ? '退出全屏' : '全屏' }}
          </el-button>
        </div>
      </template>
      <OfficeViewer
        v-if="previewVisible && previewFileUrl"
        :file-url="previewFileUrl"
        :file-name="previewFileName"
      />
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Loading, Warning, FolderOpened, FullScreen, Crop } from '@element-plus/icons-vue'
import { useUserStore } from '@/stores/user'
import WorkspaceBrowser from './WorkspaceBrowser.vue'
import SessionSkillManager from './SessionSkillManager.vue'
import OfficeViewer from './OfficeViewer.vue'

const route = useRoute()
const userStore = useUserStore()

const sessionId = computed(() => route.params.sessionId as string)

// 获取当前会话的工作区路径
const currentWorkspacePath = ref('')
const workspaceLoading = ref(false)

// 文件预览状态
const previewVisible = ref(false)
const previewFileUrl = ref('')
const previewFileName = ref('')
const isFullscreen = ref(false)

async function loadSessionWorkspace() {
  const sessionId = route.params.sessionId as string
  if (!sessionId) {
    currentWorkspacePath.value = ''
    return
  }

  workspaceLoading.value = true
  try {
    console.log('Loading workspace for session:', sessionId)
    const resp = await fetch(`/api/sessionworkspaces/sessions/${sessionId}?userId=${userStore.DEFAULT_USER_ID}`)

    if (resp.ok) {
      const data = await resp.json()
      const workDir = data.workDirectory || ''
      currentWorkspacePath.value = workDir
      console.log('Workspace loaded:', workDir)
    } else {
      const errorText = await resp.text()
      console.error('Failed to load workspace:', errorText)
      currentWorkspacePath.value = ''
    }
  } catch (err) {
    console.error('Error loading workspace:', err)
    currentWorkspacePath.value = ''
  } finally {
    workspaceLoading.value = false
  }
}

onMounted(() => {
  loadSessionWorkspace()
})

// 当会话切换时刷新工作区
watch(() => route.params.sessionId, () => {
  loadSessionWorkspace()
})

// 处理文件选择（预览）
async function handleFileSelect(filePath: string) {
  if (!filePath) return

  // 构建文件预览URL
  const fileName = filePath.split('/').pop() || filePath.split('\\').pop() || '未知文件'
  previewFileName.value = fileName

  // 使用API路径获取文件内容
  const encodedPath = encodeURIComponent(filePath)
  previewFileUrl.value = `/api/workspace/file?path=${encodedPath}&sessionId=${sessionId.value}`

  previewVisible.value = true
  console.log('Preview file:', filePath, 'URL:', previewFileUrl.value)
}
</script>

<style scoped>
.approval-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.panel-header {
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
  background: #fff;
  flex-shrink: 0;
}

.panel-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #303133;
}

.panel-content {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.loading-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 40px;
  color: #909399;
}

.workspace-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 40px;
  color: #909399;
}

.workspace-empty .el-icon {
  font-size: 32px;
}

.preview-content {
  background: #f5f7fa;
  padding: 16px;
  border-radius: 4px;
  max-height: 500px;
  overflow-y: auto;
  font-family: monospace;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-all;
  color: #606266;
}

.skill-manager-section {
  border-bottom: 1px solid #e4e7ed;
  max-height: 300px;
  overflow-y: auto;
}

.workspace-section {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.section-title {
  padding: 12px 16px;
  font-size: 14px;
  font-weight: 500;
  color: #303133;
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
}

/* 预览对话框样式 */
.preview-dialog :deep(.el-dialog__body) {
  padding: 0;
  height: 60vh;
  overflow: hidden;
}

.preview-dialog.is-fullscreen :deep(.el-dialog__body) {
  height: calc(100vh - 55px);
}

.preview-dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  padding-right: 20px;
}
</style>
