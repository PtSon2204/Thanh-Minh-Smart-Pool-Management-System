export const contractDays = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ nhật']
export const employmentTypes = [{ value: 'FULL_TIME', label: 'Toàn thời gian' }, { value: 'PART_TIME', label: 'Bán thời gian' }]
export const shiftLabel = (shift) => `${shift.name || shift.shiftName} · ${shift.startTime?.slice(0, 5)} - ${shift.endTime?.slice(0, 5)}${shift.endTime < shift.startTime ? ' (ngày sau)' : ''}`

export function weeklyHours(pattern = [], shifts = []) {
  let hours = 0
  for (const day of pattern) {
    for (const shiftId of day.shiftIds) {
      const shift = shifts.find((item) => item.id === shiftId)
      if (!shift?.startTime || !shift?.endTime) return null
      const minutes = (clock) => Number(clock.slice(0, 2)) * 60 + Number(clock.slice(3, 5))
      const duration = minutes(shift.endTime) - minutes(shift.startTime)
      hours += (duration < 0 ? duration + 1440 : duration) / 60
    }
  }
  return hours
}

export function formatHours(hours) {
  return hours === null ? 'Chưa đủ dữ liệu thời gian ca' : `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(hours)} giờ/tuần`
}

export function contractError(failure) {
  const data = failure.response?.data
  const errors = data?.errors ? Object.values(data.errors).flat().join(' ') : ''
  return errors || data?.message || 'Không thể hoàn tất thao tác. Kiểm tra kết nối và thử lại.'
}
