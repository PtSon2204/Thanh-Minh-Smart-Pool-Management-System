import apiClient from '../../../services/apiClient'

const BASE = '/api/tickets'

const ticketService = {
  /** Bán vé tại quầy (Tiền mặt) */
  sellOffline: (data) => apiClient.post(`${BASE}/sell-offline`, data).then((res) => res.data),
  
  /** Tạo đơn chờ thanh toán (Chuyển khoản) */
  createPendingOrder: (data) => apiClient.post(`${BASE}/create-pending-order`, data).then((res) => res.data),

  /** Kiểm tra trạng thái đơn hàng */
  checkOrderStatus: (orderId) => apiClient.get(`${BASE}/orders/${orderId}/status`).then((res) => res.data),
}

export default ticketService
