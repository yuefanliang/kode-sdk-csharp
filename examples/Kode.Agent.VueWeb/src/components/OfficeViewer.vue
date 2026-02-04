<template>
  <div class="office-viewer">
    <div v-if="loading" class="loading-container">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>加载中...</span>
    </div>

    <div v-else-if="error" class="error-container">
      <el-icon><Warning /></el-icon>
      <span>{{ error }}</span>
    </div>

    <div v-else class="viewer-container">
      <!-- Office 文档（doc/docx/xls/xlsx/ppt/pptx）- 纯前端预览 -->
      <div v-if="isOffice" class="office-preview">
        <div class="preview-header">
          <el-icon><Document /></el-icon>
          <span>{{ fileName }}</span>
          <div class="header-actions">
            <el-tag v-if="isExcel" type="success" size="small" effect="plain">Excel</el-tag>
            <el-tag v-else-if="isWord" type="primary" size="small" effect="plain">Word</el-tag>
            <el-tag v-else-if="isPPT" type="warning" size="small" effect="plain">PPT</el-tag>
            <el-button 
              type="primary" 
              size="small" 
              class="download-btn"
              @click="downloadFile"
            >
              <el-icon><Download /></el-icon>
              下载
            </el-button>
          </div>
        </div>
        <div class="preview-body">
          <!-- Excel 表格预览 -->
          <div v-if="isExcel && excelData.length > 0" class="excel-preview">
            <div v-for="(sheet, sIndex) in excelData" :key="sIndex" class="excel-sheet">
              <div class="sheet-name">{{ sheet.name }}</div>
              <div class="sheet-table-wrapper">
                <table class="excel-table">
                  <tbody>
                    <tr v-for="(row, rIndex) in sheet.data.slice(0, 100)" :key="rIndex">
                      <td 
                        v-for="(cell, cIndex) in row" 
                        :key="cIndex"
                        :class="{ 'header-cell': rIndex === 0 }"
                      >
                        {{ cell }}
                      </td>
                    </tr>
                  </tbody>
                </table>
                <div v-if="sheet.data.length > 100" class="more-data-hint">
                  共 {{ sheet.data.length }} 行数据，仅显示前 100 行，请下载查看完整内容
                </div>
              </div>
            </div>
          </div>
          <!-- Word/PPT 文本预览 -->
          <div v-else-if="fileContent" class="text-preview">
            <pre>{{ fileContent }}</pre>
          </div>
          <!-- Office 文件信息展示 -->
          <div v-else class="office-fallback">
            <div class="file-icon-large">
              <el-icon v-if="isExcel" :size="64" color="#217346"><Grid /></el-icon>
              <el-icon v-else-if="isWord" :size="64" color="#2B579A"><Document /></el-icon>
              <el-icon v-else-if="isPPT" :size="64" color="#D24726"><DataLine /></el-icon>
              <el-icon v-else :size="64" color="#909399"><Document /></el-icon>
            </div>
            <p class="file-name-large">{{ fileName }}</p>
            <p class="file-type">{{ ext.toUpperCase().replace('.', '') }} 文档格式</p>
            <p class="file-hint">此文件需要下载后使用相应软件打开查看</p>
            <el-button type="primary" size="large" @click="downloadFile">
              <el-icon><Download /></el-icon>
              下载文件
            </el-button>
          </div>
        </div>
      </div>

      <!-- Markdown 文件预览 -->
      <div v-else-if="isMarkdown" class="markdown-preview">
        <div class="preview-header">
          <el-icon><Document /></el-icon>
          <span>{{ fileName }}</span>
          <el-button 
            type="primary" 
            size="small" 
            class="download-btn"
            @click="downloadFile"
          >
            下载文件
          </el-button>
        </div>
        <div class="preview-body markdown-body">
          <div v-if="fileContent" class="markdown-content" v-html="renderedMarkdown"></div>
          <div v-else class="loading-text">加载中...</div>
        </div>
      </div>

      <!-- 代码/文本文件预览 -->
      <div v-else-if="isText || isCode" class="text-preview-container">
        <div class="preview-header">
          <el-icon><Document /></el-icon>
          <span>{{ fileName }}</span>
          <el-button 
            type="primary" 
            size="small" 
            class="download-btn"
            @click="downloadFile"
          >
            下载文件
          </el-button>
        </div>
        <div class="preview-body">
          <pre v-if="fileContent" class="code-content"><code>{{ fileContent }}</code></pre>
          <div v-else class="loading-text">加载中...</div>
        </div>
      </div>

      <!-- PDF 预览 -->
      <div v-else-if="ext === '.pdf'" class="pdf-preview">
        <iframe :src="resolvedFileUrl" class="pdf-frame"></iframe>
      </div>

      <!-- HTML 预览 -->
      <div v-else-if="isHtml" class="html-preview">
        <div class="preview-header">
          <el-icon><Document /></el-icon>
          <span>{{ fileName }}</span>
          <div class="header-actions">
            <el-tag type="warning" size="small" effect="plain">HTML</el-tag>
            <el-button
              type="primary"
              size="small"
              class="download-btn"
              @click="downloadFile"
            >
              <el-icon><Download /></el-icon>
              下载
            </el-button>
          </div>
        </div>
        <div class="preview-body">
          <div class="html-security-notice">
            <el-alert
              type="warning"
              :closable="false"
              show-icon
            >
              <template #title>
                <span>安全提示：HTML 页面包含脚本代码，预览时将在沙箱环境中运行</span>
              </template>
            </el-alert>
          </div>
          <iframe
            :src="resolvedFileUrl"
            class="html-frame"
            sandbox="allow-scripts allow-same-origin allow-popups allow-forms"
            referrerpolicy="no-referrer"
          ></iframe>
        </div>
      </div>

      <!-- 图片预览 -->
      <div v-else-if="isImage" class="image-preview">
        <img :src="resolvedFileUrl" alt="预览图片" />
      </div>

      <!-- 其他文件：提供下载 -->
      <div v-else class="generic-preview">
        <div class="file-info-card">
          <el-icon :size="64" color="#409EFF"><Document /></el-icon>
          <p class="file-name">{{ fileName }}</p>
          <p class="file-type">{{ ext || '未知格式' }}</p>
          <el-button type="primary" @click="downloadFile">
            <el-icon><Download /></el-icon>
            下载文件
          </el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { Loading, Warning, Document, Download, Grid, DataLine } from '@element-plus/icons-vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'

const props = defineProps<{
  fileUrl: string
  fileName: string
}>()

const loading = ref(false)
const error = ref('')
const fileContent = ref('')
const excelData = ref<{ name: string; data: any[][] }[]>([])

const ext = computed(() => getExtension(props.fileName))
const resolvedFileUrl = computed(() => {
  if (!props.fileUrl) return ''
  if (/^https?:\/\//i.test(props.fileUrl)) return props.fileUrl
  if (typeof window === 'undefined') return props.fileUrl
  return `${window.location.origin}${props.fileUrl.startsWith('/') ? '' : '/'}${props.fileUrl}`
})

const isOffice = computed(() => {
  return ['.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx'].includes(ext.value)
})

const isExcel = computed(() => {
  return ['.xls', '.xlsx'].includes(ext.value)
})

const isWord = computed(() => {
  return ['.doc', '.docx'].includes(ext.value)
})

const isPPT = computed(() => {
  return ['.ppt', '.pptx'].includes(ext.value)
})

const isImage = computed(() => {
  return ['.png', '.jpg', '.jpeg', '.gif', '.svg', '.bmp', '.webp'].includes(ext.value)
})

const isMarkdown = computed(() => {
  return ['.md', '.markdown'].includes(ext.value)
})

const isHtml = computed(() => {
  return ['.html', '.htm'].includes(ext.value)
})

const isText = computed(() => {
  return ['.txt', '.log', '.csv', '.json', '.xml'].includes(ext.value)
})

const isCode = computed(() => {
  const codeExts = ['.js', '.ts', '.jsx', '.tsx', '.vue', '.html', '.htm', '.css', '.scss', '.sass', '.less',
    '.java', '.py', '.rb', '.go', '.rs', '.php', '.c', '.cpp', '.h', '.hpp', '.cs', '.swift',
    '.sql', '.sh', '.bash', '.ps1', '.yaml', '.yml', '.ini', '.conf', '.config', '.env']
  return codeExts.includes(ext.value)
})

const renderedMarkdown = computed(() => {
  if (!fileContent.value) return ''
  const html = marked(fileContent.value, { 
    breaks: true,
    gfm: true 
  })
  return DOMPurify.sanitize(html)
})

// 解析Excel文件
async function parseExcelFile(buffer: ArrayBuffer): Promise<{ name: string; data: any[][] }[]> {
  try {
    // 动态导入 xlsx 库
    const XLSX = await import('xlsx')
    const workbook = XLSX.read(buffer, { type: 'array' })
    const result: { name: string; data: any[][] }[] = []
    
    workbook.SheetNames.forEach(sheetName => {
      const worksheet = workbook.Sheets[sheetName]
      // 转换为JSON数组，保留空单元格
      const data = XLSX.utils.sheet_to_json(worksheet, { 
        header: 1,
        defval: ''
      }) as any[][]
      
      // 过滤掉完全空的行
      const filteredData = data.filter(row => row.some(cell => cell !== '' && cell !== null && cell !== undefined))
      
      if (filteredData.length > 0) {
        result.push({ name: sheetName, data: filteredData })
      }
    })
    
    return result
  } catch (err) {
    console.error('Failed to parse Excel:', err)
    return []
  }
}

// 加载文件内容
async function loadFileContent() {
  if (!resolvedFileUrl.value) return
  
  // 只有文本类文件才加载内容
  if (!isText.value && !isCode.value && !isMarkdown.value && !isOffice.value) return
  
  loading.value = true
  error.value = ''
  fileContent.value = ''
  excelData.value = []
  
  try {
    const response = await fetch(resolvedFileUrl.value)
    if (!response.ok) throw new Error('加载失败')
    
    // Excel文件尝试解析
    if (isExcel.value) {
      try {
        const buffer = await response.arrayBuffer()
        excelData.value = await parseExcelFile(buffer)
      } catch (err) {
        console.error('Failed to parse Excel:', err)
        // Excel解析失败，显示文件信息即可
      }
    } else if (isOffice.value) {
      // Word/PPT文件显示文件信息
      fileContent.value = ''
    } else {
      const text = await response.text()
      // 限制预览大小
      fileContent.value = text.length > 100000 ? text.slice(0, 100000) + '\n\n... (内容已截断)' : text
    }
  } catch (err: any) {
    console.error('Failed to load file:', err)
    error.value = err.message || '加载文件失败'
  } finally {
    loading.value = false
  }
}

function downloadFile() {
  if (!resolvedFileUrl.value) return
  const link = document.createElement('a')
  link.href = resolvedFileUrl.value
  link.download = props.fileName
  link.target = '_blank'
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

watch(() => props.fileUrl, () => {
  loadFileContent()
})

onMounted(() => {
  loadFileContent()
})

// 获取文件扩展名
function getExtension(fileName: string): string {
  const idx = fileName.lastIndexOf('.')
  return idx > -1 ? fileName.slice(idx).toLowerCase() : ''
}
</script>

<style scoped>
.office-viewer {
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
  min-height: 200px;
}

.error-container {
  color: #f56c6c;
}

.viewer-container {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.office-preview,
.pdf-preview,
.html-preview,
.image-preview,
.generic-preview,
.markdown-preview,
.text-preview-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.preview-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
  font-size: 14px;
  font-weight: 500;
  color: #303133;
  flex-shrink: 0;
}

.download-btn {
  margin-left: auto;
}

.preview-body {
  flex: 1;
  overflow: auto;
  padding: 16px;
  background: #fff;
}

.pdf-frame,
.generic-frame {
  flex: 1;
  border: none;
  width: 100%;
  height: 100%;
}

.image-preview {
  align-items: center;
  justify-content: center;
  background: #fff;
}

.image-preview img {
  max-width: 100%;
  max-height: 80vh;
  object-fit: contain;
}

/* Office 预览样式 */
.office-fallback {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 60px 40px;
  color: #606266;
  text-align: center;
}

.office-fallback .file-name-large {
  font-size: 18px;
  font-weight: 500;
  color: #303133;
  margin: 8px 0 4px;
  word-break: break-all;
}

.office-fallback .file-type {
  font-size: 14px;
  color: #409EFF;
  font-weight: 500;
  text-transform: uppercase;
}

.office-fallback .file-hint {
  font-size: 13px;
  color: #909399;
  margin: 8px 0 16px;
}

.file-icon-large {
  margin-bottom: 8px;
}

/* Excel 预览样式 */
.excel-preview {
  padding: 16px;
}

.excel-sheet {
  margin-bottom: 24px;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  overflow: hidden;
}

.excel-sheet:last-child {
  margin-bottom: 0;
}

.sheet-name {
  padding: 12px 16px;
  background: #f0f9eb;
  color: #67c23a;
  font-weight: 600;
  font-size: 14px;
  border-bottom: 1px solid #e4e7ed;
}

.sheet-table-wrapper {
  overflow-x: auto;
  max-height: 500px;
  overflow-y: auto;
}

.excel-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.excel-table td {
  padding: 8px 12px;
  border: 1px solid #e4e7ed;
  white-space: nowrap;
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.excel-table tr:nth-child(even) {
  background-color: #fafafa;
}

.excel-table tr:hover {
  background-color: #f5f7fa;
}

.excel-table .header-cell {
  background-color: #f5f7fa;
  font-weight: 600;
  color: #303133;
  position: sticky;
  top: 0;
  z-index: 1;
}

.more-data-hint {
  padding: 12px 16px;
  text-align: center;
  color: #909399;
  font-size: 13px;
  background: #f5f7fa;
  border-top: 1px solid #e4e7ed;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-left: auto;
}

.text-preview pre {
  margin: 0;
  padding: 16px;
  background: #f5f7fa;
  border-radius: 4px;
  font-family: monospace;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 100%;
  overflow: auto;
}

.code-content {
  margin: 0;
  padding: 16px;
  background: #1e1e1e;
  color: #d4d4d4;
  border-radius: 4px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  overflow: auto;
}

.loading-text {
  text-align: center;
  color: #909399;
  padding: 40px;
}

/* Markdown 样式 */
.markdown-body {
  font-size: 14px;
  line-height: 1.6;
}

.markdown-content :deep(h1),
.markdown-content :deep(h2),
.markdown-content :deep(h3),
.markdown-content :deep(h4),
.markdown-content :deep(h5),
.markdown-content :deep(h6) {
  margin-top: 24px;
  margin-bottom: 16px;
  font-weight: 600;
  line-height: 1.25;
  color: #24292e;
}

.markdown-content :deep(h1) { font-size: 2em; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; }
.markdown-content :deep(h2) { font-size: 1.5em; border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; }
.markdown-content :deep(h3) { font-size: 1.25em; }
.markdown-content :deep(h4) { font-size: 1em; }
.markdown-content :deep(h5) { font-size: 0.875em; }
.markdown-content :deep(h6) { font-size: 0.85em; color: #6a737d; }

.markdown-content :deep(p) {
  margin-top: 0;
  margin-bottom: 16px;
}

.markdown-content :deep(code) {
  padding: 0.2em 0.4em;
  margin: 0;
  font-size: 85%;
  background-color: rgba(27, 31, 35, 0.05);
  border-radius: 3px;
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
}

.markdown-content :deep(pre) {
  padding: 16px;
  overflow: auto;
  font-size: 85%;
  line-height: 1.45;
  background-color: #f6f8fa;
  border-radius: 6px;
  margin-bottom: 16px;
}

.markdown-content :deep(pre code) {
  padding: 0;
  background-color: transparent;
}

.markdown-content :deep(ul),
.markdown-content :deep(ol) {
  padding-left: 2em;
  margin-bottom: 16px;
}

.markdown-content :deep(li) {
  margin-bottom: 0.25em;
}

.markdown-content :deep(blockquote) {
  padding: 0 1em;
  color: #6a737d;
  border-left: 0.25em solid #dfe2e5;
  margin-bottom: 16px;
}

.markdown-content :deep(table) {
  border-spacing: 0;
  border-collapse: collapse;
  margin-bottom: 16px;
  width: 100%;
  overflow: auto;
}

.markdown-content :deep(table th),
.markdown-content :deep(table td) {
  padding: 6px 13px;
  border: 1px solid #dfe2e5;
}

.markdown-content :deep(table tr:nth-child(2n)) {
  background-color: #f6f8fa;
}

.markdown-content :deep(hr) {
  height: 0.25em;
  padding: 0;
  margin: 24px 0;
  background-color: #e1e4e8;
  border: 0;
}

.markdown-content :deep(img) {
  max-width: 100%;
  box-sizing: border-box;
}

/* 通用预览样式 */
.file-info-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 60px 40px;
  text-align: center;
}

.file-info-card .file-name {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
  word-break: break-all;
}

.file-info-card .file-type {
  font-size: 13px;
  color: #909399;
  text-transform: uppercase;
}

/* HTML 预览样式 */
.html-preview {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.html-preview .preview-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 0;
  background: #f5f7fa;
  overflow: hidden;
}

.html-security-notice {
  padding: 12px 16px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  flex-shrink: 0;
}

.html-frame {
  flex: 1;
  border: none;
  width: 100%;
  background: #fff;
}
</style>
