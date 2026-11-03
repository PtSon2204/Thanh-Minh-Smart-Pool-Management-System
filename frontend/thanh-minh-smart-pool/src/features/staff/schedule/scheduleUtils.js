import dayjs from 'dayjs'

export const vietnamToday = () => {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const value = (type) => parts.find((part) => part.type === type)?.value
  return `${value('year')}-${value('month')}-${value('day')}`
}

export const mondayFor = (date) => dayjs(date).subtract((dayjs(date).day() + 6) % 7, 'day').format('YYYY-MM-DD')
export const weekDates = (monday) => Array.from({ length: 7 }, (_, index) => dayjs(monday).add(index, 'day').format('YYYY-MM-DD'))
export const formatDate = (value) => value ? dayjs(value).format('DD/MM/YYYY') : '-'
export const formatTime = (value) => value ? value.slice(0, 5) : '-'
export const isOvernight = (shift) => Boolean(shift?.startTime && shift?.endTime && shift.endTime < shift.startTime)
export const assignmentKey = (assignment) => `${assignment.employeeId}:${assignment.shiftId}:${assignment.workDate}`

export function assignmentDateError(workDate, monday) {
  if (!workDate || !dayjs(workDate).isValid()) return 'Chọn ngày làm hợp lệ.'
  if (workDate <= vietnamToday()) return 'Chỉ được phân ca từ ngày mai theo giờ Việt Nam.'
  if (!weekDates(monday).includes(workDate)) return 'Chọn ngày trong tuần đang xem.'
  return null
}

export function expandAssignmentRows(rows) {
  return rows.flatMap((row) => (row?.employeeIds || []).map((employeeId) => ({
    employeeId,
    shiftId: row.shiftId,
    workDate: row.workDate?.format('YYYY-MM-DD'),
  })))
}

export function scheduledHours(assignments) {
  const counted = new Set()
  return assignments.reduce((total, assignment) => {
    const key = assignmentKey(assignment)
    if (assignment.status === 'Cancelled' || counted.has(key) || !assignment.startTime || !assignment.endTime) return total
    counted.add(key)
    const clockHours = (value) => {
      const [hours, minutes, seconds = 0] = value.split(':').map(Number)
      return hours + minutes / 60 + seconds / 3600
    }
    const duration = clockHours(assignment.endTime) - clockHours(assignment.startTime)
    return total + (duration < 0 ? duration + 24 : duration)
  }, 0)
}

export function statusLabel(value) {
  return ({ Scheduled: 'Đã lên lịch', Present: 'Có mặt', Completed: 'Hoàn thành', Cancelled: 'Đã hủy' })[value] || value || 'Chưa xác định'
}

export function statusColor(value) {
  if (value === 'Present') return 'success'
  if (value === 'Cancelled') return 'error'
  if (value === 'Completed') return 'blue'
  return 'default'
}

function intervalFor(assignment, shifts) {
  const shift = shifts.find((item) => item.id === assignment.shiftId) || assignment
  if (!shift?.startTime || !shift?.endTime || !assignment.workDate) return null
  const start = dayjs(`${assignment.workDate}T${shift.startTime}`)
  const end = dayjs(`${assignment.workDate}T${shift.endTime}`).add(isOvernight(shift) ? 1 : 0, 'day')
  return { start, end }
}

export function conflictFor(candidate, assignments, shifts) {
  const exact = assignments.find((assignment) => assignmentKey(assignment) === assignmentKey(candidate))
  if (exact) return 'Đã có phân công cùng nhân viên, ca và ngày.'
  const candidateInterval = intervalFor(candidate, shifts)
  if (!candidateInterval) return null
  const overlap = assignments.find((assignment) => {
    if (assignment.employeeId !== candidate.employeeId || assignment.status === 'Cancelled') return false
    const interval = intervalFor(assignment, shifts)
    return interval && candidateInterval.start.isBefore(interval.end) && interval.start.isBefore(candidateInterval.end)
  })
  return overlap ? 'Nhân viên đã có ca trùng thời gian.' : null
}
