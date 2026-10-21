import { parseQrPayload, QR_PURPOSE } from "../../qr/utils/qrPurpose.js"

export function prepareTicketCode(code) {
  return parseQrPayload(code, QR_PURPOSE.ticket)
}

export function hasAcceptedEntry(items) {
  return items.some((item) => String(item.status ?? item.Status).toLowerCase() === 'allowed')
}

export function vietnamDate(date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(date)
  const value = Object.fromEntries(parts.map(({ type, value: partValue }) => [type, partValue]))
  return `${value.year}-${value.month}-${value.day}`
}

export function shouldReconcileConfirmation(error) {
  const status = error?.response?.status
  return !Number.isInteger(status) || status >= 400
}
