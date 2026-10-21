import apiClient from '../../../services/apiClient'

const base = '/api/maintenance-schedules'

export const maintenanceService = {
  async list(params, signal) { return (await apiClient.get(base, { params, signal })).data },
  async options(signal) { return (await apiClient.get(`${base}/options`, { signal })).data },
  async create(payload) { return (await apiClient.post(base, payload)).data },
  async update(id, payload) { return (await apiClient.put(`${base}/${id}`, payload)).data },
  async changeState(id, action) { await apiClient.post(`${base}/${id}/${action}`) },
  async complete(id, payload) { await apiClient.post(`${base}/${id}/complete`, payload) },
  async history(params, signal) { return (await apiClient.get(`${base}/history`, { params, signal })).data },
}
