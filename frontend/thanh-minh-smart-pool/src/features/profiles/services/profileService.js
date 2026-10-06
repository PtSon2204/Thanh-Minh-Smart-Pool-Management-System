import apiClient from '../../../services/apiClient'

const profileService = {
  async getMyProfile() {
    const response = await apiClient.get('/api/profiles/me')
    return response.data
  },

  async updateMyProfile(payload) {
    const response = await apiClient.put('/api/profiles/me', payload)
    return response.data
  },

  async uploadMyAvatar(file) {
    const formData = new FormData()
    formData.append('File', file)
    const response = await apiClient.post('/api/profiles/me/avatar', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  }
}

export default profileService
