import apiClient from '../../../services/apiClient'

const BASE = '/api/rentals'

const rentalService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  getDetails: (orderId, productId) => apiClient.get(`${BASE}/orders/${orderId}/products/${productId}`).then((response) => response.data),
  quote: (data) => apiClient.post(`${BASE}/quote`, data).then((response) => response.data),
  checkout: (data) => apiClient.post(BASE, data).then((response) => response.data),
  createQrPayment: (data) => apiClient.post(`${BASE}/payments/qr`, data).then((response) => response.data),
  getQrPayment: (id, signal) => apiClient.get(`${BASE}/payments/${id}`, { signal }).then((response) => response.data),
  getQrPayments: (params) => apiClient.get(`${BASE}/payments`, { params }).then((response) => response.data),
  issueQrPayment: (id) => apiClient.post(`${BASE}/payments/${id}/issue`).then((response) => response.data),
  cancelQrPayment: (id) => apiClient.post(`${BASE}/payments/${id}/cancel`).then((response) => response.data),
  returnRental: ({ id, data }) => apiClient.post(`${BASE}/${id}/return`, data).then((response) => response.data),
  returnQuantity: ({ orderId, productId, ...data }) => apiClient.post(`${BASE}/orders/${orderId}/products/${productId}/returns`, data).then((response) => response.data),
}

export default rentalService
