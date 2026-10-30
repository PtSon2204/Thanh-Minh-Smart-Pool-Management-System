import apiClient from '../../../services/apiClient'

const BASE = '/api/swim-classes'

export const swimClassService = {
  getClasses: (params, signal) => apiClient.get(BASE, { params, signal }).then(({ data }) => data),
  getClassById: (classId, scope, signal) => apiClient.get(`${BASE}/${classId}`, { params: scope ? { scope } : undefined, signal }).then(({ data }) => data),
  getMyRegistration: (classId, signal) => apiClient.get(`${BASE}/${classId}/registrations/me`, { signal }).then(({ data }) => data),
  getMyClasses: (params, signal) => apiClient.get(`${BASE}/registrations/me`, { params, signal }).then(({ data }) => data),
  register: (classId) => apiClient.post(`${BASE}/${classId}/registrations`).then(({ data }) => data),
  createClass: (payload) => apiClient.post(BASE, payload).then(({ data }) => data),
  updateClass: (classId, payload) => apiClient.put(`${BASE}/${classId}`, payload).then(({ data }) => data),
  createSessions: (classId, sessions) => apiClient.post(`${BASE}/${classId}/sessions`, { sessions }).then(({ data }) => data),
  updateSession: (classId, sessionId, payload) => apiClient.patch(`${BASE}/${classId}/sessions/${sessionId}`, payload).then(({ data }) => data),
  activate: (classId) => apiClient.post(`${BASE}/${classId}/activate`).then(({ data }) => data),
}
