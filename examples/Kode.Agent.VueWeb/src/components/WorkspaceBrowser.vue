<template>
  <div class="workspace-browser">
    <div v-if="loading" class="loading-container">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>加载中...</span>
    </div>

    <div v-else-if="error" class="error-container">
      <el-icon><Warning /></el-icon>
      <span>{{ error }}</span>
    </div>

    <div v-else class="browser-content">
      <!-- 当前路径导航 -->
      <div class="path-navigator">
        <el-breadcrumb separator="/">
          <el-breadcrumb-item @click="navigateToRoot">
            <el-icon><HomeFilled /></el-icon>
            工作区根目录
          </el-breadcrumb-item>
          <el-breadcrumb-item
            v-for="(segment, index) in relativePathSegments"
            :key="index"
            @click="navigateTo(index)"
          >
            {{ segment }}
          </el-breadcrumb-item>
        </el-breadcrumb>
        <div class="path-actions">
          <el-button size="small" text :disabled="!relativePath" @click="navigateToParent">返回上一级</el-button>
          <el-button size="small" text @click="refreshDirectory">刷新</el-button>
        </div>
      </div>

      <!-- 文件列表 -->
      <div class="file-list">
        <div
          v-for="item in fileList"
          :key="item.name"
          class="file-item"
          :class="{ 'is-directory': item.isDirectory, 'is-file': !item.isDirectory }"
          @click="handleItemClick(item)"
        >
          <div class="file-icon">
            <el-icon v-if="item.isDirectory"><Folder /></el-icon>
            <el-icon v-else><Document /></el-icon>
          </div>
          <div class="file-info">
            <div class="file-name">{{ item.name }}</div>
            <div class="file-meta" v-if="!item.isDirectory">
              {{ formatSize(item.size) }} · {{ formatDate(item.modified) }}
            </div>
          </div>
          <div class="file-actions" v-if="!item.isDirectory">
            <el-button
              type="primary"
              size="small"
              text
              @click.stop="handlePreview(item)"
            >
              预览
            </el-button>
          </div>
        </div>

        <el-empty
          v-if="fileList.length === 0"
          description="此目录为空"
          :image-size="60"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Loading, Warning, HomeFilled, Folder, Document } from '@element-plus/icons-vue'

interface FileItem {
  name: string
  path: string
  isDirectory: boolean
  size?: number
  modified?: string
}

const props = defineProps<{
  workspacePath?: string
}>()

const emit = defineEmits<{
  fileSelect: [filePath: string]
}>()

const loading = ref(false)
const error = ref('')
const basePath = ref('')
const relativePath = ref('')
const fileList = ref<FileItem[]>([])
let refreshTimer: ReturnType<typeof setInterval> | null = null

// 监听workspacePath变化，重新加载
watch(() => props.workspacePath, (newPath) => {
  if (newPath) {
    basePath.value = newPath
    relativePath.value = ''
    loadDirectory('')
  }
}, { immediate: true })

// 相对于基准路径的路径段（用于面包屑显示）
const relativePathSegments = computed(() => {
  if (!relativePath.value) return []
  return relativePath.value.split('/').filter(s => s.length > 0)
})

// 加载目录内容
async function loadDirectory(path: string) {
  loading.value = true
  error.value = ''

  try {
    // 只能加载相对于基准路径的目录
    const actualPath = path

    const response = await fetch(
      `/api/workspace/list?workspacePath=${encodeURIComponent(basePath.value)}&path=${encodeURIComponent(actualPath)}`
    )

    if (!response.ok) {
      throw new Error('加载目录失败')
    }

    const data = await response.json()

    if (data.error) {
      throw new Error(data.error)
    }

    fileList.value = data.files || []
    relativePath.value = data.path || actualPath
  } catch (err: any) {
    error.value = err.message || '加载失败'
    console.error('Failed to load directory:', err)
    ElMessage.error('加载目录失败')
  } finally {
    loading.value = false
  }
}

function refreshDirectory() {
  loadDirectory(relativePath.value)
}

// 处理项目点击
function handleItemClick(item: FileItem) {
  if (item.isDirectory) {
    // 进入子目录
    const newPath = relativePath.value
      ? `${relativePath.value}/${item.name}`
      : item.name
    loadDirectory(newPath)
  } else {
    // 文件，触发预览事件
    emit('fileSelect', item.path)
  }
}

// 导航到指定路径段（相对于基准路径）
function navigateTo(index: number) {
  const segments = relativePathSegments.value.slice(0, index + 1)
  const newPath = segments.join('/')
  loadDirectory(newPath)
}

function navigateToRoot() {
  loadDirectory('')
}

function navigateToParent() {
  if (!relativePath.value) return
  const segments = relativePathSegments.value.slice(0, -1)
  const newPath = segments.join('/')
  loadDirectory(newPath)
}

// 预览文件
function handlePreview(item: FileItem) {
  emit('fileSelect', item.path)
}

// 格式化文件大小
function formatSize(bytes?: number): string {
  if (!bytes) return '未知'
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

// 格式化日期
function formatDate(dateStr?: string): string {
  if (!dateStr) return ''
  try {
    const date = new Date(dateStr)
    const yyyy = date.getFullYear()
    const mm = String(date.getMonth() + 1).padStart(2, '0')
    const dd = String(date.getDate()).padStart(2, '0')
    return `${yyyy}-${mm}-${dd}`
  } catch {
    return dateStr
  }
}

onMounted(() => {
  // 默认加载根目录
  loadDirectory('')
  refreshTimer = setInterval(() => {
    if (!loading.value && basePath.value) {
      refreshDirectory()
    }
  }, 5000)
})

onUnmounted(() => {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
})
</script>

<style scoped>
.workspace-browser {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.loading-container,
.error-container {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px;
  color: #909399;
}

.error-container {
  color: #f56c6c;
}

.browser-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.path-navigator {
  padding: 12px;
  border-bottom: 1px solid #e4e7ed;
  background: #f5f7fa;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.path-navigator :deep(.el-breadcrumb__item) {
  cursor: pointer;
}

.path-navigator :deep(.el-breadcrumb__item:hover) {
  color: #409eff;
}

.path-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.file-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  margin-bottom: 4px;
  background: #fff;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.file-item:hover {
  background: #f5f7fa;
}

.file-item.is-directory {
  background: #f0f9ff;
}

.file-item.is-directory:hover {
  background: #e6f7ff;
}

.file-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 4px;
  background: #f5f7fa;
}

.file-item.is-directory .file-icon {
  color: #409eff;
  background: #e6f7ff;
}

.file-item.is-file .file-icon {
  color: #606266;
}

.file-info {
  flex: 1;
  min-width: 0;
}

.file-name {
  font-size: 14px;
  font-weight: 500;
  color: #303133;
  margin-bottom: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-meta {
  font-size: 12px;
  color: #909399;
}

.file-actions {
  flex-shrink: 0;
}

.file-actions :deep(.el-button) {
  padding: 4px 8px;
}
</style>
