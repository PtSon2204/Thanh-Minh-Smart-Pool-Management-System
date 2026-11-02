import apiClient from '../../../services/apiClient'

const BASE = '/api/coach-applications'

export const coachApplicationService = {
  getMine: (signal) => apiClient.get(`${BASE}/me`, { signal }).then(({ data }) => data || null),
  getMyRequests: (params, signal) => apiClient.get(`${BASE}/me/requests`, { params, signal }).then(({ data }) => data),
  submit: (certificate) => {
    const body = new FormData()
    body.append('certificate', certificate)
    return apiClient.post(BASE, body, { headers: { 'Content-Type': undefined } }).then(({ data }) => data)
  },
  getList: (params, signal) => apiClient.get(BASE, { params, signal }).then(({ data }) => data),
  getById: (id, signal) => apiClient.get(`${BASE}/${id}`, { signal }).then(({ data }) => data),
  getCertificate: (id, signal) => apiClient.get(`${BASE}/${id}/certificate`, { signal }).then(({ data }) => data),
  review: (id, decision, note) => apiClient.post(`${BASE}/${id}/review`, { decision, note }).then(({ data }) => data),
}
