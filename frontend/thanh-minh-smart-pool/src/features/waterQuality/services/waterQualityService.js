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

  /**
   * Xem lịch trực bể theo bộ lọc
   * @param {Object} params - { fromDate, toDate, shiftId, employeeId, status, pageIndex, pageSize }
   */
  getDutySchedules: (params = {}) =>
    apiClient.get(`${BASE}/duty-schedules`, { params }).then((res) => res.data),

  /**
   * Lấy danh sách nhân viên trực bể & danh sách ca trực
   */
  getDutyScheduleOptions: () =>
    apiClient.get(`${BASE}/duty-schedules/options`).then((res) => res.data),

  /**
   * Tự động gán lịch trực bể
   * @param {Object} data - { fromDate, toDate, shiftIds, candidateEmployeeIds, overwriteExisting, prioritizeShiftRoster, defaultNotes }
   */
  autoAssignDutySchedules: (data) =>
    apiClient.post(`${BASE}/duty-schedules/auto-assign`, data).then((res) => res.data),

  /**
   * Tạo hoặc gán lịch trực bể thủ công
   * @param {Object} data - { dutyDate, shiftId, assignedEmployeeId, status, notes, waterQualityLogId }
   */
  saveDutySchedule: (data) =>
    apiClient.post(`${BASE}/duty-schedules`, data).then((res) => res.data),

  /**
   * Cập nhật lịch trực bể
   * @param {string} id
   * @param {Object} data - { dutyDate, shiftId, assignedEmployeeId, status, notes, waterQualityLogId }
   */
  updateDutySchedule: (id, data) =>
    apiClient.put(`${BASE}/duty-schedules/${id}`, data).then((res) => res.data),
};
