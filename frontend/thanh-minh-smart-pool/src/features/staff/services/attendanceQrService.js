import apiClient from '../../../services/apiClient'

const BASE = '/api/staff-attendance/qr-sessions'

export const attendanceQrService = {
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
  rotate: (sessionId, generation) => apiClient.post(`${BASE}/${sessionId}/rotate`, { generation }).then((response) => response.data),
  stop: (sessionId) => apiClient.delete(`${BASE}/${sessionId}`).then((response) => response.data),
  preview: (qrPayload) => apiClient.post('/api/staff-attendance/preview', { qrPayload }).then((response) => response.data),
  scan: (qrPayload) => apiClient.post('/api/staff-attendance/scan', { qrPayload }).then((response) => response.data),
}
