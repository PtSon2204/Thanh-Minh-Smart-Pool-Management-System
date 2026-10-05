import apiClient from '../../../services/apiClient'

const BASE = '/api/services'

const serviceService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
  update: (id, data) => apiClient.put(`${BASE}/${id}`, data).then((response) => response.data),
  setStatus: (id, isActive) => apiClient.patch(`${BASE}/${id}/status`, { isActive }).then((response) => response.data),
  adjustStock: (id, data) => apiClient.post(`${BASE}/${id}/stock-adjustments`, data).then((response) => response.data),
  getInventoryHistory: (id, params) => apiClient.get(`${BASE}/${id}/inventory-history`, { params }).then((response) => response.data),
}

export default serviceService
