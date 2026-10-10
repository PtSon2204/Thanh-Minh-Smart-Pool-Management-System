import apiClient from '../../../services/apiClient';

const BASE = '/api/v1/notifications';

export const notificationService = {
  /**
   * Lấy danh sách thông báo
   * @param {Object} params - { page, pageSize, type, isRead }
   */
  getNotifications: (params = {}) =>
    apiClient.get(BASE, { params }).then((res) => res.data),

  /**
   * Đánh dấu đã đọc
   * @param {string} id - notification ID
   */
  markAsRead: (id) =>
    apiClient.patch(`${BASE}/${id}/read`).then((res) => res.data),

  /**
   * Tạo thông báo mới (internal / test)
   */
  createNotification: (data) =>
    apiClient.post(BASE, data).then((res) => res.data),

  /**
   * Gửi email thông báo tự động tới khách hàng
   * @param {Object} data - { toEmail, recipientName, subject, templateType, content, metadata }
   */
  sendEmailNotification: (data) =>
    apiClient.post(`${BASE}/email/send`, data).then((res) => res.data),

  /**
   * Xem trước nội dung HTML mẫu email
   */
  previewEmail: (data) =>
    apiClient.post(`${BASE}/email/preview`, data, { responseType: 'text' }).then((res) => res.data),

  /**
   * Lấy lịch sử email đã gửi
   */
  getEmailHistory: (limit = 50) =>
    apiClient.get(`${BASE}/email/history`, { params: { limit } }).then((res) => res.data),
};
