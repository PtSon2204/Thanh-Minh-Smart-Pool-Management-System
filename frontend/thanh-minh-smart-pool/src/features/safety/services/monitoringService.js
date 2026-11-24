import apiClient from '../../../services/apiClient'

export const monitoringService = {
  cameras: (params, signal) => apiClient.get('/api/cameras', { params, signal }).then(r => r.data),
  areas: signal => apiClient.get('/api/cameras/areas', { signal }).then(r => r.data),
  alerts: (params, signal) => apiClient.get('/api/alerts', { params, signal }).then(r => r.data),
  alertFilters: signal => apiClient.get('/api/alerts/filters', { signal }).then(r => r.data),
  handleAlert: (id, action, expectedSeverity) => apiClient.patch(`/api/alerts/${id}/${action}`,
    { expectedSeverity }).then(({ data }) => {
    // Fallback cho client xác nhận nếu SignalR mất kết nối; dedupe theo IncidentId.
    if (action === 'acknowledge' && typeof window !== 'undefined') window.dispatchEvent(new CustomEvent('safety:acknowledged', { detail: data }))
    return data
  }).catch(error => {
    const current = error.response?.data?.alert
    if (error.response?.status === 409 && current && typeof window !== 'undefined')
      window.dispatchEvent(new CustomEvent('safety:alert-refreshed', { detail: current }))
    throw error
  }),
  alert: (id, signal) => apiClient.get(`/api/alerts/${id}`, { signal }).then(r => r.data),
  analysis: (id, signal) => apiClient.get(`/api/cameras/${id}/analysis`, { signal }).then(r => r.data),
  thresholds: {
    get: signal => apiClient.get('/api/thresholds/safety-monitoring', { signal }).then(r => r.data),
    save: payload => apiClient.put('/api/thresholds/safety-monitoring', payload).then(r => r.data),
  },
}
