if (import.meta.env.DEV) {
  import("react-grab");
  import("react-scan");
}

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import "./features/qr/utils/prepareZXingModule.js"
import './index.css'
import App from './App.jsx'
import './styles/responsive.css'

// Bắt các lỗi JavaScript toàn cục nếu script bị crash trước khi React mount
window.addEventListener('error', (event) => {
  console.error('[Global Error]', event.error || event.message)
  const root = document.getElementById('root')
  if (root && (!root.innerHTML || root.innerHTML.trim() === '')) {
    root.innerHTML = `
      <div style="min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f8fafc; font-family: sans-serif; padding: 20px;">
        <div style="max-width: 650px; width: 100%; background: #ffffff; padding: 30px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); border-left: 6px solid #ef4444;">
          <h2 style="color: #dc2626; margin-top: 0;">Lỗi khởi chạy giao diện (Script Error)</h2>
          <p style="color: #475569;">${event.message || 'Lỗi không xác định khi tải JavaScript'}</p>
          <pre style="background: #f1f5f9; padding: 12px; border-radius: 6px; font-size: 12px; overflow: auto; max-height: 200px; color: #b91c1c;">${event.error?.stack || event.filename + ':' + event.lineno}</pre>
          <button onclick="sessionStorage.clear(); localStorage.clear(); window.location.reload();" style="margin-top: 15px; padding: 10px 18px; background: #0284c7; color: white; border: none; border-radius: 6px; cursor: pointer; font-weight: 600;">
            Xóa dữ liệu phiên & Tải lại
          </button>
        </div>
      </div>
    `
  }
})

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 1000 * 60 * 5, // 5 phút
    },
  },
})

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
