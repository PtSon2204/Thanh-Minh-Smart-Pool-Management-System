export const USER_ROLES = [
  { value: 'CUSTOMER', label: 'Khách hàng' },
  { value: 'STAFF', label: 'Nhân viên' },
  { value: 'ADMIN', label: 'Quản trị viên' },
]

export function getUserError(error) {
  const data = error?.response?.data
  const firstFieldError = Object.values(data?.errors || {}).flat()[0]
  if (error?.response?.status === 403) return data?.detail || 'Bạn không có quyền thực hiện thao tác này.'
  return data?.detail || firstFieldError || data?.title || 'Không thể kết nối hoặc xử lý yêu cầu. Vui lòng thử lại.'
}
