import apiClient from '../../../services/apiClient';

const BASE = '/api/v1/reports';

export const reportService = {
  /**
   * Lấy dữ liệu báo cáo thống kê khách hàng
   * @param {Object} params - { period: 'today' | '7days' | '30days' | 'month' | 'custom', fromDate, toDate }
   */
  getCustomerStatistics: (params = {}) =>
    apiClient.get(`${BASE}/customer-statistics`, { params }).then((res) => res.data),

  /**
   * Lấy dữ liệu thống kê doanh thu bán vé
   * @param {Object} params - { period: 'today' | '7days' | '30days' | 'month' | 'custom', fromDate, toDate }
   */
  getTicketRevenueStatistics: (params = {}) =>
    apiClient.get(`${BASE}/ticket-revenue`, { params }).then((res) => res.data),

  /**
   * Lấy dữ liệu báo cáo thống kê vé tháng & hội viên
   * @param {Object} params - { filter: 'all' | 'active' | 'expiring_soon' | 'expired' }
   */
  getMonthlyTicketStatistics: (params = {}) =>
    apiClient.get(`${BASE}/monthly-tickets`, { params }).then((res) => res.data),

  /**
   * Lấy dữ liệu báo cáo thống kê sự cố vận hành & an toàn bể bơi
   * @param {Object} params - { filter: 'all' | 'critical_urgent' | 'in_progress' }
   */
  getIncidentStatistics: (params = {}) =>
    apiClient.get(`${BASE}/incidents`, { params }).then((res) => res.data),
};

