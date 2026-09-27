import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Load .env để đọc VITE_API_BASE_URL ngay trong config
  const env = loadEnv(mode, process.cwd(), '')

  // Lấy URL gốc của backend (bỏ phần path /api/v1 để proxy hoạt động đúng)
  const backendUrl = (env.VITE_API_BASE_URL ?? 'http://localhost:5000')
    .replace(/\/api.*$/, '') // cắt bỏ /api/v1 nếu có

  const signalrUrl = (env.VITE_SIGNALR_HUB_URL ?? 'http://localhost:5000')
    .replace(/\/hubs.*$/, '')

  return {
    plugins: [react()],

    // Dev Server
    server: {
      port: 5173,
      strictPort: true, // Báo lỗi ngay nếu port đã bị chiếm, không tự đổi port
      cors: false,      // Tắt CORS của Vite — proxy sẽ lo phần này

      
      // Proxy: forward request từ FE sang BE
      // browser chỉ thấy localhost:5173 (cùng origin)
      proxy: {
        // Tất cả request đến /api/** → backend
        '/api': {
          target: backendUrl,
          changeOrigin: true,   // Đổi header Origin cho khớp với target
          secure: false,        // Chấp nhận HTTPS tự ký (self-signed cert)
          rewrite: (path) => path, // Giữ nguyên path /api/...
        },

        // Request đến /hubs/** → backend (SignalR WebSocket)
        '/hubs': {
          target: signalrUrl,
          changeOrigin: true,
          secure: false,
          ws: true,             // Bật proxy cho WebSocket (SignalR cần)
        },
      },
    },

    // Build
    build: {
      outDir: 'dist',
      sourcemap: false,
    },
  }
})
