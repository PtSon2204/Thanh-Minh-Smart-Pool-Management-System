export function classAvailability(swimClass, now = Date.now()) {
  if (swimClass.status === 'Draft') return { kind: 'draft', text: 'Lớp đang là bản nháp và chưa mở đăng ký.' }
  if (swimClass.status !== 'Active') return { kind: 'inactive', text: 'Lớp hiện không còn hoạt động.' }
  if (now < Date.parse(swimClass.registrationStartAt)) return { kind: 'upcoming', text: 'Chưa đến thời gian nhận đăng ký.' }
  if (now >= Date.parse(swimClass.registrationEndAt)) return { kind: 'closed', text: 'Thời gian nhận đăng ký đã kết thúc.' }
  if (swimClass.confirmedStudentCount >= swimClass.capacity) return { kind: 'full', text: 'Lớp đã đủ số học viên.' }
  return { kind: 'open', text: 'Lớp đang trong thời gian nhận đăng ký.' }
}

export function canActivateClass(swimClass, now = Date.now()) {
  if (swimClass.status !== 'Draft') return false
  const starts = swimClass.sessions
    .filter(session => session.status === 'Scheduled' && Date.parse(session.startsAt) > now)
    .map(session => Date.parse(session.startsAt))
  return starts.length > 0 && Date.parse(swimClass.registrationEndAt) <= Math.min(...starts)
}
