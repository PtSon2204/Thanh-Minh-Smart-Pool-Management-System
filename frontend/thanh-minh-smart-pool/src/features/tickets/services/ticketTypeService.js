import apiClient from '../../../services/apiClient'

const BASE = '/api/ticket-types'

const ticketTypeService = {
  /** Lấy danh sách tất cả loại vé */
  getAll: () => apiClient.get(BASE).then((res) => res.data),

  /** Tạo mới loại vé */
  create: (data) => apiClient.post(BASE, data).then((res) => res.data),
}

export default ticketTypeService
