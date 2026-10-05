import apiClient from '../../../services/apiClient'

const BASE = '/api/rentals'

const rentalService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  getDetails: (orderId, productId) => apiClient.get(`${BASE}/orders/${orderId}/products/${productId}`).then((response) => response.data),
  checkout: (data) => apiClient.post(BASE, data).then((response) => response.data),
  returnRental: (id) => apiClient.post(`${BASE}/${id}/return`).then((response) => response.data),
  returnQuantity: ({ orderId, productId, rentalIds }) => apiClient.post(`${BASE}/orders/${orderId}/products/${productId}/returns`, { rentalIds }).then((response) => response.data),
}

export default rentalService
