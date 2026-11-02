import axios from 'axios'
import apiClient from '../../../services/apiClient'

const base = '/api/staffs'
// Invitation credentials do not use the normal session or its automatic retry.
const invitationClient = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL, timeout: 30000 })
const body = (response) => response.data

export const onboardingService = {
  list: (page = 1) => apiClient.get(`${base}/invitations`, { params: { page, pageSize: 20 } }).then(body),
  create: (data) => apiClient.post(`${base}/invitations`, data).then(body),
  detail: (id) => apiClient.get(`${base}/invitations/${id}`).then(body),
  invitationAction: (id, action, data) => apiClient.post(`${base}/invitations/${id}/${action}`, data).then(body),
  inspect: (credentials) => invitationClient.post(`${base}/onboarding/inspect`, credentials).then(body),
  own: () => apiClient.get(`${base}/me/onboarding`).then(body),
  requestCorrection: (data) => apiClient.post(`${base}/me/onboarding/correction-requests`, data).then(body),
  openCorrection: (employeeId, data) => apiClient.post(`${base}/${employeeId}/onboarding/corrections`, data).then(body),
  employeeDetail: (employeeId) => apiClient.get(`${base}/${employeeId}/onboarding`).then(body),
  review: (revisionId, action, data) => apiClient.post(`${base}/onboarding/revisions/${revisionId}/${action}`, data).then(body),
  save: (credentials, data) => credentials
    ? invitationClient.post(`${base}/onboarding/draft`, { ...credentials, ...data }).then(body)
    : apiClient.post(`${base}/me/onboarding/draft`, data).then(body),
  submit: (credentials, data) => credentials
    ? invitationClient.post(`${base}/onboarding/submit`, { ...credentials, ...data }).then(body)
    : apiClient.post(`${base}/me/onboarding/submit`, data).then(body),
  upload: (credentials, expectedVersion, purpose, file) => {
    const data = new FormData()
    if (credentials) {
      data.append('invitationId', credentials.invitationId)
      data.append('token', credentials.token)
    }
    data.append('expectedVersion', expectedVersion)
    data.append('purpose', purpose)
    data.append('file', file)
    const client = credentials ? invitationClient : apiClient
    const path = credentials ? '/onboarding/documents' : '/me/onboarding/documents'
    return client.post(`${base}${path}`, data, { headers: { 'Content-Type': undefined } }).then(body)
  },
  readDocument: (id, credentials, signal) => credentials
    ? invitationClient.post(`${base}/onboarding/documents/${id}/read`, credentials, { responseType: 'blob', signal }).then(body)
    : apiClient.get(`${base}/onboarding/documents/${id}`, { responseType: 'blob', signal }).then(body),
  readReviewDocument: (id, signal) => apiClient.get(`${base}/onboarding/review-documents/${id}`, { responseType: 'blob', signal }).then(body),
}
