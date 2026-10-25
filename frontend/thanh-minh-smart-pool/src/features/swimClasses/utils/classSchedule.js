import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc.js'
import timezone from 'dayjs/plugin/timezone.js'

dayjs.extend(utc)
dayjs.extend(timezone)

export const VIETNAM_TIMEZONE = 'Asia/Ho_Chi_Minh'

// DatePicker stores the time displayed in its local fields. Interpret those
// fields as Vietnam time, then send an explicit UTC instant to the API.
export function vietnamPickerTimeToUtc(value) {
  return dayjs.tz(value.format('YYYY-MM-DD HH:mm:ss'), VIETNAM_TIMEZONE).toISOString()
}

export function formatVietnamTime(value) {
  return dayjs(value).tz(VIETNAM_TIMEZONE).format('DD/MM/YYYY HH:mm')
}

export function findRequestedOverlap(sessions) {
  const intervals = sessions.map((session) => ({
    startsAt: vietnamPickerTimeToUtc(session.startsAt),
    endsAt: vietnamPickerTimeToUtc(session.endsAt),
  }))
  for (let index = 0; index < intervals.length; index += 1) {
    for (let earlier = 0; earlier < index; earlier += 1) {
      if (intervals[index].startsAt < intervals[earlier].endsAt &&
          intervals[earlier].startsAt < intervals[index].endsAt) {
        return { index, earlier }
      }
    }
  }
  return null
}
