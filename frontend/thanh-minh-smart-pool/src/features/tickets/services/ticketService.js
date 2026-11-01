import apiClient from '../../../services/apiClient'

const BASE = '/api/tickets'

const ticketService = {
  checkoutOnline: (data) => apiClient.post(`${BASE}/checkout`, data).then((res) => res.data),

  checkMyOrderStatus: (orderId, signal) => apiClient.get(`${BASE}/me/orders/${orderId}/status`, { signal }).then((res) => res.data),

  getMyOrders: ({ pageIndex = 1, pageSize = 10, status = 'ALL', signal } = {}) =>
    apiClient.get(`${BASE}/me/orders`, { params: { pageIndex, pageSize, status }, signal }).then((res) => res.data),

  getMyOrdersSummary: (signal) => apiClient.get(`${BASE}/me/orders/summary`, { signal }).then((res) => res.data),

  /** Bán vé tại quầy (Tiền mặt) */
  sellOffline: (data) => apiClient.post(`${BASE}/sell-offline`, data).then((res) => res.data),
  
  /** Tạo đơn chờ thanh toán (Chuyển khoản) */
  createPendingOrder: (data) => apiClient.post(`${BASE}/create-pending-order`, data).then((res) => res.data),

  /** Kiểm tra trạng thái đơn hàng */
  checkOrderStatus: (orderId) => apiClient.get(`${BASE}/orders/${orderId}/status`).then((res) => res.data),

  /** Cập nhật mã ngắn 6 chữ số của vé */
  updateCode: (ticketId, code) => apiClient.put(`${BASE}/${ticketId}/code`, { code }).then((res) => res.data),

  /** Lấy danh sách vé của tôi */
  getMyTickets: () => apiClient.get(`${BASE}/me`).then((res) => res.data),

  /** Lấy lịch sử quét vào bể của tôi */
  getMyEntryHistory: () => apiClient.get(`${BASE}/me/entries`).then((res) => res.data),
}

export default ticketService
