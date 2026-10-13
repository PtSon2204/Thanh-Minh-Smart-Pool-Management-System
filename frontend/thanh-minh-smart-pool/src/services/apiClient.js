import axios from 'axios'
import { getAccessToken, useAuthStore } from '../features/auth/store/authStore'

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

apiClient.interceptors.request.use((config) => {
  const isAuthRequest = ['/api/auth/login', '/api/auth/register'].includes(config.url)
  const token = getAccessToken()
  if (token && !isAuthRequest) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401) {
      const url = error.config?.url || ''
      if (!url.includes('/api/auth/login')) {
        useAuthStore.getState().setSession(null)
      }
    }
    return Promise.reject(error)
  }
)

export default apiClient
