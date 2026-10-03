import apiClient from '../../../services/apiClient'

const BASE = '/api/vouchers'

const voucherService = {
  getAll:      (params)      => apiClient.get(BASE, { params }).then(r => r.data),
  create:      (data)        => apiClient.post(BASE, data).then(r => r.data),
  update:      (id, data)    => apiClient.put(`${BASE}/${id}`, data).then(r => r.data),
  toggleLock:  (id)          => apiClient.put(`${BASE}/${id}/toggle-lock`).then(r => r.data),
  delete:      (id)          => apiClient.delete(`${BASE}/${id}`).then(r => r.data),
}

export default voucherService
