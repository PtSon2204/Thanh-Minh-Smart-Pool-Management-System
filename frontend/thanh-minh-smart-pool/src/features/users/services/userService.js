import apiClient from '../../../services/apiClient'

const BASE = '/api/users'

const userService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  getById: (id) => apiClient.get(`${BASE}/${id}`).then((response) => response.data),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
  update: (id, data) => apiClient.put(`${BASE}/${id}`, data).then((response) => response.data),
  changeRole: (id, role) => apiClient.patch(`${BASE}/${id}/role`, { role }).then((response) => response.data),
  changeStatus: (id, status) => apiClient.patch(`${BASE}/${id}/status`, { status }).then((response) => response.data),
}

export default userService
