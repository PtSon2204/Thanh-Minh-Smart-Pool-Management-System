import { formatVietnamTime } from './classSchedule'

export function coachName(coaches, coachUserId) {
  const coach = coaches.find((item) => item.userId === coachUserId)
  return coach?.fullName || coach?.username || coach?.email || coachUserId?.slice(0, 8) || 'Chưa chọn'
}

export function conflictDescription(conflict, coaches) {
  const reason = {
    request_overlap: `trùng với buổi ${Number(conflict.conflictingRequestIndex) + 1} trong danh sách đang nhập`,
    class_overlap: 'trùng với một buổi đã lưu của lớp này',
    coach_overlap: 'Coach đã được phân công cho một lớp khác',
    pool_overlap: 'bể đã có lớp khác trong khung giờ này',
  }[conflict.reason] || 'bị trùng lịch'
  return `Buổi ${Number(conflict.sessionIndex) + 1} · ${formatVietnamTime(conflict.startsAt)}–${formatVietnamTime(conflict.endsAt).slice(-5)} · ${coachName(coaches, conflict.coachUserId)}: ${reason}.`
}

export function classServerMessage(error, fallback) {
  if (error?.response?.status === 401 || error?.response?.status === 403) {
    return 'Bạn không có quyền thực hiện thao tác này.'
  }
  return error?.response?.data?.message || fallback
}

export function applyClassServerErrors(form, error) {
  const errors = error?.response?.data?.errors
  if (!errors || typeof errors !== 'object') return
  form.setFields(Object.entries(errors).map(([key, messages]) => {
    const sessionField = /^Sessions\[(\d+)\]\.(\w+)$/.exec(key)
    const name = sessionField
      ? ['sessions', Number(sessionField[1]), sessionField[2][0].toLowerCase() + sessionField[2].slice(1)]
      : [key[0].toLowerCase() + key.slice(1)]
    return { name, errors: Array.isArray(messages) ? messages : [String(messages)] }
  }))
}
