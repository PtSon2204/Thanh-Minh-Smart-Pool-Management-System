import axios from 'axios'

/**
 * Axios instance dùng chung toàn app.
 * Base URL lấy từ biến môi trường VITE_API_BASE_URL.
 */
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: import.meta.env.VITE_API_TIMEOUT ?? 30000,
  headers: {
    'Content-Type': 'application/json',
  },
})

export default apiClient
