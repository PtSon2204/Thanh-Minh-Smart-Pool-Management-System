export const equipmentStatuses = {
  Active: { label: 'Đang hoạt động', color: 'success' },
  Maintenance: { label: 'Đang bảo trì', color: 'warning' },
  Inactive: { label: 'Đã khóa', color: 'default' },
}

export const cameraStates = {
  Connected: { label: 'Đã kết nối', color: 'success' },
  Connecting: { label: 'Đang kết nối', color: 'processing' },
  Disconnected: { label: 'Mất kết nối', color: 'default' },
  Error: { label: 'Lỗi kết nối', color: 'error' },
}

export function isCamera(category) {
  return category?.trim().toLowerCase() === 'camera'
}

