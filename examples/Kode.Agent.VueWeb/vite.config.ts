import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5123',
        changeOrigin: true
      },
      '/v1': {
        target: 'http://localhost:5123',
        changeOrigin: true
      },
      '/healthz': {
        target: 'http://localhost:5123',
        changeOrigin: true
      }
    }
  }
})
