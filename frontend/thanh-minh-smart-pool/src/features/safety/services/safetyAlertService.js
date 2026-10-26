import apiClient from '../../../services/apiClient'

const BASE = '/api/safety-alerts'

const safetyAlertService = {
  /**
   * Kích hoạt cảnh báo cứu hộ thủ công.
   * Backend sẽ broadcast SignalR event "safety_alert" tới tất cả clients.
   */
  activate: (payload) =>
    apiClient.post(`${BASE}/activate`, payload).then((r) => r.data),

  /**
   * Lấy danh sách lịch sử sự cố & cảnh báo cứu hộ (hỗ trợ phân trang, lọc tìm kiếm).
   */
  getIncidents: (params) =>
    apiClient.get(`${BASE}/incidents`, { params }).then((r) => r.data),

  /**
   * Xác nhận xử lý sự cố cứu hộ.
   */
  confirmIncident: (id, payload) =>
    apiClient.put(`${BASE}/incidents/${id}/confirm`, payload).then((r) => r.data),
}

export default safetyAlertService
