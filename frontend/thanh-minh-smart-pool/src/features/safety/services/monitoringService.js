import apiClient from '../../../services/apiClient'

export const monitoringService = {
  cameras: (params, signal) => apiClient.get('/api/cameras', { params, signal }).then(r => r.data),
  areas: signal => apiClient.get('/api/cameras/areas', { signal }).then(r => r.data),
  alerts: (params, signal) => {
    if (params.sourceType === 'UPLOAD' && params.jobId) {
      return apiClient.get(`/api/video-analyses/${params.jobId}/alerts`, {
        params: { page: params.page || 1, pageSize: params.pageSize || 20 }, signal,
      }).then(r => r.data)
    }
    return apiClient.get('/api/alerts', { params, signal }).then(r => r.data)
  },
  handleAlert: (id, action) => apiClient.patch(`/api/alerts/${id}/${action}`).then(r => r.data),
  alert: (id, signal) => apiClient.get(`/api/alerts/${id}`, { signal }).then(r => r.data),
  analysis: (id, signal) => apiClient.get(`/api/cameras/${id}/analysis`, { signal }).then(r => r.data),
  analysisFrame: (id, frameId, signal) => apiClient.get(`/api/cameras/${id}/analysis/frame`, { params: { frameId }, responseType: 'blob', signal }).then(r => r.data),
  thresholds: {
    get: signal => apiClient.get('/api/thresholds/safety-monitoring', { signal }).then(r => r.data),
    save: payload => apiClient.put('/api/thresholds/safety-monitoring', payload).then(r => r.data),
  },
  status: signal => apiClient.get('/api/video-analyses/status', { signal }).then(r => r.data),
  upload: (file, onUploadProgress) => { const body = new FormData(); body.append('file', file); return apiClient.post('/api/video-analyses', body,
    { headers: { 'Content-Type': 'multipart/form-data' }, onUploadProgress, timeout: 120000 }).then(r => r.data) },
  job: (id, signal) => apiClient.get(`/api/video-analyses/${id}`, { signal }).then(r => r.data),
  playback: (id, signal) => apiClient.get(`/api/video-analyses/${id}/playback`, {
    baseURL: import.meta.env.DEV ? '' : apiClient.defaults.baseURL, signal,
  }).then(({ data }) => ({
    ...data,
    url: new URL(data.streamPath, import.meta.env.DEV ? window.location.origin : apiClient.defaults.baseURL || window.location.origin).href,
  })),
}
