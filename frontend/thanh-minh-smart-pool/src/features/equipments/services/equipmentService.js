import apiClient from '../../../services/apiClient'

const equipmentService = {
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
