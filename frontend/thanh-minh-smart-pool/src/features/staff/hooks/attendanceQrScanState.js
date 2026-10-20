export function normalizeAttendanceAction(action) {
  if (action === 0 || action === 'CheckIn') return 'CheckIn'
  if (action === 1 || action === 'CheckOut') return 'CheckOut'
  return null
}

export function isQrPreviewActive(preview, now) {
  const tokenExpiry = Date.parse(preview?.tokenExpiresAtUtc)
  const sessionExpiry = Date.parse(preview?.sessionExpiresAtUtc)
  return Number.isFinite(tokenExpiry) && Number.isFinite(sessionExpiry) && tokenExpiry > now && sessionExpiry > now
}

export function shouldShowExpiredQrNotice(phase, preview, tokenIsValid) {
  return Boolean(preview && phase !== 'complete' && !tokenIsValid)
}

export function findPreviewAssignment(assignments, preview) {
  return assignments.find((item) => item.shiftId === preview.shiftId && item.workDate === preview.workDate) ?? null
}

export function reconcileAttendanceScan(assignments, preview) {
  const assignment = findPreviewAssignment(assignments, preview)
  if (!assignment) return 'inconsistent'

  if (preview.action === 'CheckIn') {
    if ((assignment.status === 'Present' || assignment.status === 'Completed') && assignment.checkInTime) return 'recorded'
    if (assignment.status === 'Scheduled' && !assignment.checkInTime && !assignment.checkOutTime) return 'unchanged'
    return 'inconsistent'
  }

  if (assignment.status === 'Completed' && assignment.checkInTime && assignment.checkOutTime) return 'recorded'
  if (assignment.status === 'Present' && assignment.checkInTime && !assignment.checkOutTime) return 'unchanged'
  return 'inconsistent'
}
