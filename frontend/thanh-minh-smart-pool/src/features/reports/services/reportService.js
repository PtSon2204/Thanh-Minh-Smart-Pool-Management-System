import apiClient from '../../../services/apiClient';

const BASE = '/api/v1/reports';

export const reportService = {
  /**
   * Lấy dữ liệu báo cáo thống kê khách hàng
   * @param {Object} params - { period: 'today' | '7days' | '30days' | 'month' | 'custom', fromDate, toDate }
   */
  getCustomerStatistics: (params = {}) =>
    apiClient.get(`${BASE}/customer-statistics`, { params }).then((res) => res.data),
};
