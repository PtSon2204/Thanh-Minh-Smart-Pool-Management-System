import apiClient from '../../../services/apiClient'

const BASE = '/api/pool-access'

const poolAccessService = {
  lookup: (code) => apiClient.post(`${BASE}/lookup`, { code }).then((response) => response.data),
  confirm: (code, inputMode, entriesRequested = 1) => apiClient.post(`${BASE}/entries`, { code, inputMode, entriesRequested }).then((response) => response.data),
  getEntries: (ticketId, date) => apiClient.get(`${BASE}/entries`, { params: { ticketId, date, pageIndex: 1, pageSize: 20 } }).then((response) => response.data),
  getOccupancy: () => apiClient.get(`${BASE}/occupancy`).then((response) => response.data),
  getLiveStatus: () => apiClient.get(`${BASE}/live-status`).then((response) => response.data),
  updateOperationalConfig: (payload) => apiClient.put(`${BASE}/operational-config`, payload).then((response) => response.data),
  toggleMaintenance: (status, reason) => apiClient.post(`${BASE}/toggle-maintenance`, { status, reason }).then((response) => response.data),
}

export { poolAccessService }
export default poolAccessService
