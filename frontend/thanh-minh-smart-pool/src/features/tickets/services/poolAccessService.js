import apiClient from '../../../services/apiClient'

const BASE = '/api/pool-access'

const poolAccessService = {
  lookup: (code) => apiClient.post(`${BASE}/lookup`, { code }).then((response) => response.data),
  confirm: (code, inputMode) => apiClient.post(`${BASE}/entries`, { code, inputMode }).then((response) => response.data),
  getEntries: (ticketId, date) => apiClient.get(`${BASE}/entries`, { params: { ticketId, date, pageIndex: 1, pageSize: 20 } }).then((response) => response.data),
}

export default poolAccessService
