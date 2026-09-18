import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const certDir = path.resolve(__dirname, '../backend/certs')
const certFile = path.join(certDir, 'cert.pem')
const keyFile = path.join(certDir, 'key.pem')

// 本地 HTTPS：复用 scripts/gen_cert.py 生成的自签证书（SAN 含 localhost/127.0.0.1）。
// 证书不存在时回退 HTTP，保证未生成证书也能启动。
const useHttps = fs.existsSync(certFile) && fs.existsSync(keyFile)
const https = useHttps
  ? { key: fs.readFileSync(keyFile), cert: fs.readFileSync(certFile) }
  : undefined

export default defineConfig({
  plugins: [vue()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    https,
    proxy: {
      '/api': {
        target: 'https://127.0.0.1:8000',
        changeOrigin: true,
        secure: false, // 容忍后端本地自签证书
      },
    },
  },
})
