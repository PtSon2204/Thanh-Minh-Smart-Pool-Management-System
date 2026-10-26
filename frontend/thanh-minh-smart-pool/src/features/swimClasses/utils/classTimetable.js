import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc.js'
import timezone from 'dayjs/plugin/timezone.js'
import { VIETNAM_TIMEZONE } from './classSchedule.js'

dayjs.extend(utc)
dayjs.extend(timezone)

const WEEKDAY_LABELS = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật']

export function weekStartFor(value) {
  const date = dayjs(value).tz(VIETNAM_TIMEZONE)
  return date.subtract((date.day() + 6) % 7, 'day').format('YYYY-MM-DD')
}

export function recommendedWeekStart(sessions, now) {
  const validSessions = sessions.filter(session => session.status !== 'Cancelled')
  const upcoming = validSessions.find(session => Date.parse(session.startsAt) >= now)
  return weekStartFor(upcoming?.startsAt || validSessions.at(-1)?.startsAt || sessions.at(-1)?.startsAt || now)
}

export function shiftWeek(weekStart, amount) {
  return dayjs.tz(weekStart, VIETNAM_TIMEZONE).add(amount * 7, 'day').format('YYYY-MM-DD')
}

export function weekDays(weekStart, sessions, now) {
  const start = dayjs.tz(weekStart, VIETNAM_TIMEZONE)
  const todayKey = dayjs(now).tz(VIETNAM_TIMEZONE).format('YYYY-MM-DD')
  return WEEKDAY_LABELS.map((label, index) => {
    const day = start.add(index, 'day')
    const dateKey = day.format('YYYY-MM-DD')
    return {
      label,
      dateKey,
      dateLabel: day.format('DD/MM'),
      isToday: dateKey === todayKey,
      sessions: sessions.filter(session =>
        dayjs(session.startsAt).tz(VIETNAM_TIMEZONE).format('YYYY-MM-DD') === dateKey),
    }
  })
}

export function weekRangeLabel(weekStart) {
  const start = dayjs.tz(weekStart, VIETNAM_TIMEZONE)
  return `${start.format('DD/MM/YYYY')} – ${start.add(6, 'day').format('DD/MM/YYYY')}`
}

export function sessionTimeRange(session) {
  const start = dayjs(session.startsAt).tz(VIETNAM_TIMEZONE)
  const end = dayjs(session.endsAt).tz(VIETNAM_TIMEZONE)
  return `${start.format('HH:mm')} – ${end.format(start.isSame(end, 'day') ? 'HH:mm' : 'DD/MM HH:mm')}`
}
