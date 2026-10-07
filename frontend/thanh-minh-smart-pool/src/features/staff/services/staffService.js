import apiClient from '../../../services/apiClient'

const BASE = '/api/staffs'

const staffService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  getOptions: () => apiClient.get(`${BASE}/options`).then((response) => response.data),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
  update: (userId, data) => apiClient.put(`${BASE}/${userId}`, data).then((response) => response.data),
  getSchedule: (params) => apiClient.get(`${BASE}/schedule`, { params }).then((response) => response.data),
  getSalaries: (params) => apiClient.get(`${BASE}/salaries`, { params }).then((response) => response.data),
}

export default staffService
