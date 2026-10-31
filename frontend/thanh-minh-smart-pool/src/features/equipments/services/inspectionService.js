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
  async resolveItem(inspectionId, equipmentId, { action, note }, signal) {
    const { data } = await apiClient.patch(
      `${base}/${inspectionId}/items/${equipmentId}/resolve`,
      { action, note },
      { signal }
    )
    return data
  },
}

export default inspectionService
