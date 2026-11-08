import apiClient from '../../../services/apiClient'

const path = (employeeId) => `/api/staffs/${employeeId}/work-pattern`
const body = (response) => response.data

export const workPatternService = {
  get: (employeeId) => apiClient.get(path(employeeId)).then(body),
  preview: (employeeId, data) => apiClient.post(`${path(employeeId)}/preview`, data).then(body),
  confirm: (employeeId, data) => apiClient.post(`${path(employeeId)}/confirm`, data).then(body),
  previewException: (employeeId, data) => apiClient.post(`${path(employeeId)}/exceptions/preview`, data).then(body),
  confirmException: (employeeId, data) => apiClient.post(`${path(employeeId)}/exceptions/confirm`, data).then(body),
  operation: (employeeId, operationId) => apiClient.get(`${path(employeeId)}/operations/${operationId}`).then(body),
}
