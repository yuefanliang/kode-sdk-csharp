<template>
  <div id="app">
    <el-container class="app-container">
      <el-header class="app-header">
        <div class="header-content">
          <h1 class="logo">Kode Agent</h1>
          <WorkspaceSelector />
        </div>
      </el-header>
      <el-container>
        <el-aside width="260px" class="app-sidebar">
          <SessionList />
        </el-aside>
        <el-main class="app-main">
          <router-view />
        </el-main>
        <el-aside width="300px" class="app-right-panel">
          <ApprovalPanel />
        </el-aside>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useUserStore } from './stores/user'
import { useWorkspaceStore } from './stores/workspace'
import WorkspaceSelector from './components/WorkspaceSelector.vue'
import SessionList from './components/SessionList.vue'
import ApprovalPanel from './components/ApprovalPanel.vue'

const userStore = useUserStore()
const workspaceStore = useWorkspaceStore()

onMounted(async () => {
  // 默认用户初始化
  await userStore.initDefaultUser()
  // 加载工作区
  await workspaceStore.loadWorkspaces()
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
}

.app-sidebar {
  background: #f5f7fa;
  border-right: 1px solid #e4e7ed;
  padding: 10px;
}

.app-main {
  background: #fff;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.app-right-panel {
  background: #f5f7fa;
  border-left: 1px solid #e4e7ed;
  padding: 10px;
  overflow-y: auto;
}
</style>
