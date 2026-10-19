import axios from 'axios'
import { refreshResponseSchema } from '../features/auth/types/authSchemas'
import {
  getAccessToken,
  hasValidRefreshToken,
  useAuthStore,
} from '../features/auth/store/authStore'

const clientOptions = {
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: import.meta.env.VITE_API_TIMEOUT ?? 30000,
  headers: { 'Content-Type': 'application/json' },
}

const apiClient = axios.create(clientOptions)
const tokenClient = axios.create(clientOptions)
let refreshPromise = null

function isPublicAuthRequest(url = '') {
  return ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout',
    '/api/auth/register', '/api/auth/register/verify', '/api/auth/register/resend'].includes(url)
    || url.startsWith('/api/auth/password-reset/')
}

function clearIfCurrent(refreshToken) {
  const store = useAuthStore.getState()
  if (store.session?.refreshToken === refreshToken) store.setSession(null)
}

export function refreshSession() {
  if (refreshPromise) return refreshPromise

  const session = useAuthStore.getState().session
  if (!hasValidRefreshToken(session)) {
    if (session) useAuthStore.getState().setSession(null)
    return Promise.reject(new Error('Phiên đăng nhập đã hết hạn.'))
  }

  const originalToken = session.refreshToken
  refreshPromise = tokenClient.post('/api/auth/refresh', { refreshToken: originalToken })
    .then(({ data }) => {
      const tokens = refreshResponseSchema.parse(data)
      const current = useAuthStore.getState().session
      if (current?.refreshToken !== originalToken) {
        throw new Error('Phiên đăng nhập đã thay đổi.')
      }
      const updated = { ...current, ...tokens }
      useAuthStore.getState().setSession(updated)
      return updated
    })
    .catch((error) => {
      if (error?.response?.status === 401 || error?.name === 'ZodError') {
        clearIfCurrent(originalToken)
      }
      throw error
    })
    .finally(() => { refreshPromise = null })
  return refreshPromise
}

apiClient.interceptors.request.use(async (config) => {
  if (isPublicAuthRequest(config.url)) return config

  let token = getAccessToken()
  if (!token && hasValidRefreshToken(useAuthStore.getState().session)) {
    await refreshSession()
    token = getAccessToken()
  }
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`)
    config._authToken = token
  }
  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const config = error?.config
    if (error?.response?.status !== 401 || !config || isPublicAuthRequest(config.url)) {
      return Promise.reject(error)
    }
    if (config._authRetry) {
      const current = useAuthStore.getState().session
      if (current?.accessToken === config._authToken) {
        useAuthStore.getState().setSession(null)
      }
      return Promise.reject(error)
    }

    if (!hasValidRefreshToken(useAuthStore.getState().session)) {
      useAuthStore.getState().setSession(null)
      return Promise.reject(error)
    }

    try {
      // A concurrent request may already have rotated the token.
      if (!getAccessToken() || getAccessToken() === config._authToken) {
        await refreshSession()
      }
      const token = getAccessToken()
      if (!token) throw error
      config._authRetry = true
      config.headers.set('Authorization', `Bearer ${token}`)
      return apiClient(config)
    } catch (refreshError) {
      return Promise.reject(refreshError)
    }
  },
)

export default apiClient
