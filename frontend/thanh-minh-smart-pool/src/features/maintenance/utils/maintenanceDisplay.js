import dayjs from 'dayjs'

export const maintenanceStatuses = {
  Pending: { label: 'Chờ thực hiện', color: 'processing' },
  InProgress: { label: 'Đang thực hiện', color: 'warning' },
  Completed: { label: 'Hoàn thành', color: 'success' },
  Cancelled: { label: 'Đã hủy', color: 'default' },
  Paused: { label: 'Tạm dừng', color: 'default' },
}

export const recurrenceOptions = [
  { value: 'None', label: 'Một lần' },
  { value: 'Day', label: 'Theo ngày' },
  { value: 'Week', label: 'Theo tuần' },
  { value: 'Month', label: 'Theo tháng' },
]

export function recurrenceText(item) {
  if (item.repeatUnit === 'None') return 'Một lần'
  return `Mỗi ${item.repeatEvery} ${{ Day: 'ngày', Week: 'tuần', Month: 'tháng' }[item.repeatUnit] || ''}`
}

export function vietnamToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const get = type => parts.find(part => part.type === type).value
  return `${get('year')}-${get('month')}-${get('day')}`
}

export function previewDates(startDate, repeatUnit, repeatEvery) {
  if (!startDate?.isValid() || !repeatUnit || !Number.isInteger(repeatEvery) || repeatEvery < 1) return []
  if (repeatUnit === 'None') return [startDate.format('DD/MM/YYYY')]
  const unit = { Day: 'day', Week: 'week', Month: 'month' }[repeatUnit]
  return [0, 1, 2].map(index => dayjs(startDate).add(index * repeatEvery, unit).format('DD/MM/YYYY'))
}

export function setMaintenanceErrors(form, error) {
  const errors = error?.response?.data?.errors
  if (!errors || typeof errors !== 'object') return
  form.setFields(Object.entries(errors).flatMap(([key, messages]) => {
    const field = key.split('.').at(-1)
    const name = field.charAt(0).toLowerCase() + field.slice(1)
    return ['equipmentId', 'taskName', 'repeatUnit', 'repeatEvery', 'startDate', 'assignedTo', 'remindBeforeDays', 'cost', 'vendorName', 'note'].includes(name)
      ? [{ name, errors: Array.isArray(messages) ? messages : [String(messages)] }] : []
  }))
}
