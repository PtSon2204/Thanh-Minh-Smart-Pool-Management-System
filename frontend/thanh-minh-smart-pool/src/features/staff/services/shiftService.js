import apiClient from '../../../services/apiClient'

const BASE = '/api/shifts'
const PAGE_SIZE = 100

async function readAllActivePages(signal) {
  for (let attempt = 0; attempt < 2; attempt += 1) {
    const firstPage = await shiftService.getAll({ pageIndex: 1, pageSize: PAGE_SIZE, isActive: true }, signal)
    const totalCount = firstPage.totalCount ?? 0
    const pages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
    const items = [...(firstPage.items || [])]
    let isStable = true
    for (let pageIndex = 2; pageIndex <= pages; pageIndex += 1) {
      const page = await shiftService.getAll({ pageIndex, pageSize: PAGE_SIZE, isActive: true }, signal)
      if (page.totalCount !== totalCount) {
        isStable = false
        break
      }
      items.push(...(page.items || []))
    }
    const ids = new Set(items.map((item) => item.id))
    if (isStable && items.length === totalCount && ids.size === items.length) return { items, totalCount }
  }
  throw new Error('Danh sách ca làm việc không ổn định. Hãy thử lại.')
}

const shiftService = {
  getAll: (params, signal) => apiClient.get(BASE, { params, signal }).then((response) => response.data),
  getAllActive: (signal) => readAllActivePages(signal),
  create: (data) => apiClient.post(BASE, data).then((response) => response.data),
}

export default shiftService
