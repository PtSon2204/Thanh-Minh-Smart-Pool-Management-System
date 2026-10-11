import apiClient from '../../../services/apiClient'

const BASE = '/api/payments'

const paymentService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((r) => r.data),
  getById: (id) => apiClient.get(`${BASE}/${id}`).then((r) => r.data),
}

export default paymentService
