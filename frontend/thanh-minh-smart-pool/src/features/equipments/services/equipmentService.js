import apiClient from '../../../services/apiClient'

const equipmentService = {
  async getCameraViewConfiguration(id, signal) {
    const { data } = await apiClient.get(`/api/equipments/${id}/camera/views`, { signal })
    return data
  },
  async openCameraView(id, offer, signal) {
    const { data } = await apiClient.post(`/api/equipments/${id}/camera/views`, offer, { signal })
    return data
  },
  async getCameraViewStatus(id, viewId, signal) {
    const { data } = await apiClient.get(`/api/equipments/${id}/camera/views/${viewId}`, { signal })
    return data
  },
  async closeCameraView(id, viewId) {
    await apiClient.delete(`/api/equipments/${id}/camera/views/${viewId}`)
  },
  async getCameraStatus(id, signal) {
    const { data } = await apiClient.get(`/api/equipments/${id}/camera`, { signal })
    return data
  },
  async create(payload) {
    const { data } = await apiClient.post('/api/equipments', payload)
    return data
  },
  async getAll(params, signal) {
    const { data } = await apiClient.get('/api/equipments', { params, signal })
    return data
  },
  async getFilters(signal) {
    const { data } = await apiClient.get('/api/equipments/filters', { signal })
    return data
  },
}

export default equipmentService
