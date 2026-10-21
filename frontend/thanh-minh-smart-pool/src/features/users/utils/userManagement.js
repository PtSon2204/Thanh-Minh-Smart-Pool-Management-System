export const USER_ROLES = [
  { value: 'CUSTOMER', label: 'Khách hàng' },
  { value: 'ADMIN', label: 'Quản trị viên' },
  { value: 'LIFEGUARD', label: 'Nhân viên cứu hộ' },
  { value: 'SALES', label: 'Nhân viên bán vé' },
]

export const USER_ROLE_COLORS = { ADMIN: 'purple', CUSTOMER: 'cyan', LIFEGUARD: 'blue', SALES: 'geekblue' }

export function getUserError(error) {
  const data = error?.response?.data
  const firstFieldError = Object.values(data?.errors || {}).flat()[0]
  if (error?.response?.status === 403) return data?.detail || 'Bạn không có quyền thực hiện thao tác này.'
  return data?.detail || data?.message || firstFieldError || data?.title || 'Không thể kết nối hoặc xử lý yêu cầu. Vui lòng thử lại.'
}
