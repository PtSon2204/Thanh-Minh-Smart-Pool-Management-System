import apiClient from '../../../services/apiClient'

const BASE = '/api/vouchers'

const voucherService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
}

export default voucherService
