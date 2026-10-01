import apiClient from '../../../services/apiClient'

const BASE = '/api/ticket-types'

const ticketTypeService = {
  /** Lấy danh sách loại vé  */
  getAll: (params) => apiClient.get(BASE, { params }).then((res) => res.data),

  /** Tạo mới loại vé */
  create: (data) => apiClient.post(BASE, data).then((res) => res.data),

  /** Lấy chi tiết loại vé */
  getById: (id) => apiClient.get(`${BASE}/${id}`).then((res) => res.data),

  /** Cập nhật loại vé */
  update: (id, data) => apiClient.put(`${BASE}/${id}`, data).then((res) => res.data),

  /** Khóa / Mở khóa loại vé */
  toggleLock: (id) => apiClient.put(`${BASE}/${id}/toggle-lock`).then((res) => res.data),
}

export default ticketTypeService
