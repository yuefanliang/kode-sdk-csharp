<template>
  <div id="app">
    <el-container class="app-container">
      <el-header class="app-header">
        <div class="header-content">
          <h1 class="logo" @click="$router.push('/chat')">智码工坊</h1>
          <div class="header-actions">
            <el-button text @click="$router.push('/settings')">
              <el-icon><Setting /></el-icon>设置
            </el-button>
          </div>
        </div>
      </el-header>
      <el-container class="app-body">
        <el-aside width="260px" class="app-sidebar">
          <SessionList />
        </el-aside>
        <el-main class="app-main">
          <router-view />
        </el-main>
        <div
          v-if="showRightPanel"
          class="resize-handle"
          @mousedown="startResize"
          :class="{ 'is-resizing': isResizing }"
        ></div>
        <el-aside v-if="showRightPanel" :width="rightPanelWidth + 'px'" class="app-right-panel">
          <ApprovalPanel />
        </el-aside>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { Setting } from '@element-plus/icons-vue'
import { useUserStore } from './stores/user'
import SessionList from './components/SessionList.vue'
import ApprovalPanel from './components/ApprovalPanel.vue'

const userStore = useUserStore()
const route = useRoute()

// 只在聊天页面显示右侧面板
const showRightPanel = computed(() => route.path.startsWith('/chat'))

// 右侧面板宽度
const rightPanelWidth = ref(300)
const isResizing = ref(false)
const minWidth = 200
const maxWidth = 600

function startResize(e: MouseEvent) {
  isResizing.value = true
  const startX = e.clientX
  const startWidth = rightPanelWidth.value

  function handleMouseMove(e: MouseEvent) {
    if (!isResizing.value) return
    const delta = startX - e.clientX
    const newWidth = Math.max(minWidth, Math.min(maxWidth, startWidth + delta))
    rightPanelWidth.value = newWidth
  }

  function handleMouseUp() {
    isResizing.value = false
    document.removeEventListener('mousemove', handleMouseMove)
    document.removeEventListener('mouseup', handleMouseUp)
  }

  document.addEventListener('mousemove', handleMouseMove)
  document.addEventListener('mouseup', handleMouseUp)
}

onMounted(async () => {
  // 默认用户初始化
  await userStore.initDefaultUser()
})
</script>

<style scoped>
.app-container {
  height: 100vh;
  display: flex;
  flex-direction: column;
}

.app-header {
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  padding: 0 20px;
  display: flex;
  align-items: center;
  height: 60px;
  flex-shrink: 0;
}

.header-content {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.logo {
  font-size: 20px;
  font-weight: 600;
  color: #409eff;
  margin: 0;
  cursor: pointer;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 16px;
}

.app-body {
  flex: 1;
  overflow: hidden;
}

.app-sidebar {
  background: #f5f7fa;
  border-right: 1px solid #e4e7ed;
  padding: 10px;
  overflow: hidden;
}

.app-main {
  background: #fff;
  padding: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.app-right-panel {
  background: #f5f7fa;
  border-left: 1px solid #e4e7ed;
  padding: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.resize-handle {
  width: 6px;
  cursor: col-resize;
  background: transparent;
  transition: background 0.2s;
  flex-shrink: 0;
}

.resize-handle:hover,
.resize-handle.is-resizing {
  background: #409eff;
}
</style>
