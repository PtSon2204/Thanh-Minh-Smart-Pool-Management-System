import apiClient from '../../../services/apiClient'

const BASE = '/api/swim-classes'

export const swimClassService = {
  getClasses: (params, signal) => apiClient.get(BASE, { params, signal }).then(({ data }) => data),
  getClassById: (classId, scope, signal) => apiClient.get(`${BASE}/${classId}`, { params: scope ? { scope } : undefined, signal }).then(({ data }) => data),
  getMyRegistration: (classId, signal) => apiClient.get(`${BASE}/${classId}/registrations/me`, { signal }).then(({ data }) => data),
  getMyClasses: (params, signal) => apiClient.get(`${BASE}/registrations/me`, { params, signal }).then(({ data }) => data),
  register: (classId) => apiClient.post(`${BASE}/${classId}/registrations`).then(({ data }) => data),
  cancelMyRegistration: (classId) => apiClient.patch(`${BASE}/${classId}/registrations/me/cancel`).then(({ data }) => data),
  getClassRegistrations: (classId, params, signal) => apiClient.get(`${BASE}/${classId}/registrations`, { params, signal }).then(({ data }) => data),
  cancelClassRegistration: (classId, registrationId, payload) => apiClient.patch(`${BASE}/${classId}/registrations/${registrationId}/cancel`, payload).then(({ data }) => data),
  getMyClassAttendances: (classId, params, signal) => apiClient.get(`${BASE}/${classId}/attendances/me`, { params, signal }).then(({ data }) => data),
  submitClassAttendance: (classId, sessionId, image) => {
    const body = new FormData()
    body.append('image', image)
    return apiClient.post(`${BASE}/${classId}/sessions/${sessionId}/attendances/me`, body,
      { headers: { 'Content-Type': undefined } }).then(({ data }) => data)
  },
  getSessionAttendances: (classId, sessionId, params, signal) => apiClient.get(
    `${BASE}/${classId}/sessions/${sessionId}/attendances`, { params, signal }).then(({ data }) => data),
  reviewClassAttendance: (classId, sessionId, attendanceId, payload) => apiClient.patch(
    `${BASE}/${classId}/sessions/${sessionId}/attendances/${attendanceId}/review`, payload).then(({ data }) => data),
  createClass: (payload) => apiClient.post(BASE, payload).then(({ data }) => data),
  updateClass: (classId, payload) => apiClient.put(`${BASE}/${classId}`, payload).then(({ data }) => data),
  createSessions: (classId, sessions) => apiClient.post(`${BASE}/${classId}/sessions`, { sessions }).then(({ data }) => data),
  updateSession: (classId, sessionId, payload) => apiClient.patch(`${BASE}/${classId}/sessions/${sessionId}`, payload).then(({ data }) => data),
  cancelSession: (classId, sessionId, payload) => apiClient.patch(`${BASE}/${classId}/sessions/${sessionId}/cancel`, payload).then(({ data }) => data),
  activate: (classId) => apiClient.post(`${BASE}/${classId}/activate`).then(({ data }) => data),
  deactivate: (classId, payload) => apiClient.patch(`${BASE}/${classId}/deactivate`, payload).then(({ data }) => data),
}
