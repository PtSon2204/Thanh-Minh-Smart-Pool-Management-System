export const coachApplicationStatus = {
  Pending: { label: 'Đang chờ duyệt', color: 'processing' },
  Approved: { label: 'Đã duyệt', color: 'success' },
  Rejected: { label: 'Đã từ chối', color: 'error' },
  Revoked: { label: 'Đã thu hồi quyền', color: 'default' },
}

export function formatCoachApplicationDate(value) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short',
  }).format(new Date(value))
}

export function getCoachApplicationError(error) {
  return error?.response?.data?.message || 'Không thể hoàn tất thao tác. Vui lòng thử lại.'
}
