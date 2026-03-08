import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { execSync } from 'child_process'

const commitHash = (() => {
  try {
    return execSync('git rev-parse --short HEAD').toString().trim()
  } catch {
    return 'unknown'
  }
})()

const gitVersion = (() => {
  try {
    return execSync('git describe --tags --abbrev=0').toString().trim().replace(/^v/, '')
  } catch {
    return process.env.npm_package_version || '0.0.0'
  }
})()

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  define: {
    __APP_VERSION__: JSON.stringify(gitVersion),
    __COMMIT_HASH__: JSON.stringify(commitHash)
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5079',
        changeOrigin: true
      }
    }
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts']
  }
})
