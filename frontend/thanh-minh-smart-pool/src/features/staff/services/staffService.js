import apiClient from '../../../services/apiClient'

const BASE = '/api/staffs'
const PAGE_SIZE = 100

class UnstableScheduleReadError extends Error {
  constructor() {
    super('Dữ liệu lịch thay đổi khi đang tải. Hãy thử lại.')
    this.name = 'UnstableScheduleReadError'
  }
}

async function readAllPages(requestPage, signal) {
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const firstPage = await requestPage(1, signal)
    const totalCount = firstPage.totalCount ?? 0
    const pages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
    const items = [...(firstPage.items || [])]
    let isStable = true

    for (let pageIndex = 2; pageIndex <= pages; pageIndex += 1) {
      const page = await requestPage(pageIndex, signal)
      if (page.totalCount !== totalCount) {
        isStable = false
        break
      }
      items.push(...(page.items || []))
    }

    const ids = new Set(items.map((item) => item.id))
    if (isStable && items.length === totalCount && ids.size === items.length) return { items, totalCount }
  }

  throw new UnstableScheduleReadError()
}

function getSchedulePage(path, params, signal) {
  return apiClient.get(path, { params, signal }).then((response) => response.data)
}

const staffService = {
  getAll: (params) => apiClient.get(BASE, { params }).then((response) => response.data),
  getOptions: () => apiClient.get(`${BASE}/options`).then((response) => response.data),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
  update: (userId, data) => apiClient.put(`${BASE}/${userId}`, data).then((response) => response.data),
  getSchedule: (params, signal) => getSchedulePage(`${BASE}/schedule`, params, signal),
  getMySchedule: (params, signal) => getSchedulePage(`${BASE}/me/schedule`, params, signal),
  getCompleteSchedule: (scope, params, signal) => {
    const path = scope === 'all' ? `${BASE}/schedule` : `${BASE}/me/schedule`
    const supportedParams = scope === 'all'
      ? { fromDate: params.fromDate, toDate: params.toDate, employeeId: params.employeeId, status: params.status }
      : { fromDate: params.fromDate, toDate: params.toDate }
    return readAllPages((pageIndex) => getSchedulePage(path, { ...supportedParams, pageIndex, pageSize: PAGE_SIZE }, signal), signal)
  },
  getScheduleEmployees: (signal) => apiClient.get(`${BASE}/schedule/employees`, { signal }).then((response) => response.data),
  assignShiftBatch: (assignments) => apiClient.post(`${BASE}/schedule/batch`, { assignments }).then((response) => response.data),
  cancelSchedule: (id) => apiClient.delete(`${BASE}/schedule/${id}`).then((response) => response.data),
  checkIn: (id) => apiClient.post(`${BASE}/schedule/${id}/check-in`).then((response) => response.data),
  checkOut: (id) => apiClient.post(`${BASE}/schedule/${id}/check-out`).then((response) => response.data),
}

export default staffService
