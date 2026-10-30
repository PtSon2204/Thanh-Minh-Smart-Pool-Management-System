import apiClient from '../../../services/apiClient';

const BASE = '/api/v1/water-quality-logs';

export const waterQualityService = {
  /**
   * Lấy danh sách phiếu đo có phân trang và lọc
   * @param {Object} params - { pageIndex, pageSize, fromDate, toDate, status, inspectedBy }
   */
  getLogs: (params = {}) =>
    apiClient.get(BASE, { params }).then((res) => res.data),

  /**
   * Lấy dữ liệu phân tích chỉ số nước & xu hướng
   * @param {Object} params - { fromDate, toDate }
   */
  getAnalytics: (params = {}) =>
    apiClient.get(`${BASE}/analytics`, { params }).then((res) => res.data),

  /**
   * Xem trước kết quả đánh giá và gợi ý hóa chất theo pH, Clo, độ trong
   * @param {Object} data - { phValue, freeChlorineMgL, isWaterClear }
   */
  previewEvaluation: (data) =>
    apiClient.post(`${BASE}/preview`, data).then((res) => res.data),

  /**
   * Xem chi tiết phiếu đo theo ID
   * @param {string} id
   */
  getLogById: (id) =>
    apiClient.get(`${BASE}/${id}`).then((res) => res.data),

  /**
   * Tạo mới phiếu đo
   * @param {Object} data - { inspectionTime, phValue, freeChlorineMgL, isWaterClear, notes, equipmentId }
   */
  createLog: (data) =>
    apiClient.post(BASE, data).then((res) => res.data),
};
