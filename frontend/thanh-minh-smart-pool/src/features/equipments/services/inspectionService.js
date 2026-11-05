import apiClient from '../../../services/apiClient'

const base = '/api/equipment-inspections'

export const inspectionService = {
  async getForm(signal) {
    const { data } = await apiClient.get(`${base}/form`, { signal })
    return data
  },
  async submit(payload) {
    const { data } = await apiClient.post(base, payload)
    return data
  },
  async getInspections(params, signal) {
    const { data } = await apiClient.get(base, { params, signal })
    return data
  },
  async uploadImage(file, signal) {
    const formData = new FormData()
    formData.append('file', file)
    const { data } = await apiClient.post(`${base}/images`, formData, {
      signal,
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  },
  async resolveItem(inspectionId, equipmentId, { action, note, resolutionImageUrls }, signal) {
    const { data } = await apiClient.patch(
      `${base}/${inspectionId}/items/${equipmentId}/resolve`,
      { action, note, resolutionImageUrls },
      { signal }
    )
    return data
  },
}

export default inspectionService
