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
};
