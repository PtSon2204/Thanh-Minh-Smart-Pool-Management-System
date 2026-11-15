const DAY_IN_MS = 24 * 60 * 60 * 1000

export function canSubmitClassAttendance(item, now = Date.now()) {
  if (item.registrationStatus !== 'Confirmed' || item.sessionStatus !== 'Scheduled' || item.attendance) return false
  const startsAt = Date.parse(item.startsAt)
  const endsAt = Date.parse(item.endsAt)
  return Number.isFinite(startsAt) && Number.isFinite(endsAt) && startsAt <= now && now <= endsAt + DAY_IN_MS
}

export const attendanceStatus = {
  PendingReview: { label: 'Chờ Coach duyệt', color: 'processing' },
  Verified: { label: 'Đã xác nhận', color: 'success' },
  Rejected: { label: 'Bị từ chối', color: 'error' },
}
