import apiClient from '../../../services/apiClient'
import { loginResponseSchema } from '../types/authSchemas'

const authService = {
  async login(payload) {
    const response = await apiClient.post('/api/auth/login', payload)
    return loginResponseSchema.parse(response.data)
  },

  async register(payload) {
    await apiClient.post('/api/auth/register', payload)
  },
}

export default authService
