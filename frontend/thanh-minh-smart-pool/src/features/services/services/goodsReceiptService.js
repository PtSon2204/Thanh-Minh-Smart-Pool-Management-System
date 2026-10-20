import apiClient from '../../../services/apiClient'

const BASE = '/api/goods-receipts'

export const goodsReceiptService = {
  getAll: (params) => apiClient.get(BASE, { params }).then(({ data }) => data),
  getById: (id) => apiClient.get(`${BASE}/${id}`).then(({ data }) => data),
  create: (data) => apiClient.post(BASE, data).then(({ data }) => data),
  post: (id) => apiClient.post(`${BASE}/${id}/post`).then(({ data }) => data),
  cancel: (id) => apiClient.post(`${BASE}/${id}/cancel`).then(({ data }) => data),
}
