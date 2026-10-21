export const QR_PURPOSE = Object.freeze({
  ticket: 'ticket',
  staff: 'staff',
})

export const STAFF_QR_PREFIX = 'SMARTPOOL:STAFF:V1:'

const RESERVED_STAFF_PREFIX = 'SMARTPOOL:STAFF:'
const TICKET_QR_MAX_LENGTH = 255
const STAFF_QR_MAX_LENGTH = 512

function rejected(reason) {
  return { accepted: false, reason }
}

export function parseQrPayload(payload, expectedPurpose) {
  if (typeof payload !== 'string' || payload.trim().length === 0) {
    return rejected('Mã QR không có dữ liệu hợp lệ.')
  }

  if (expectedPurpose === QR_PURPOSE.ticket) {
    if (payload.length > TICKET_QR_MAX_LENGTH) {
      return rejected('Mã vé vượt quá độ dài cho phép.')
    }
    if (payload.startsWith(RESERVED_STAFF_PREFIX)) {
      return rejected('Mã QR này chỉ dùng để chấm công nhân viên.')
    }
    return { accepted: true, payload }
  }

  if (expectedPurpose === QR_PURPOSE.staff) {
    if (payload.length > STAFF_QR_MAX_LENGTH || !payload.startsWith(STAFF_QR_PREFIX) || payload.length === STAFF_QR_PREFIX.length) {
      return rejected('Mã QR này không phải mã chấm công hợp lệ.')
    }
    return { accepted: true, payload }
  }

  return rejected('Chế độ quét QR không hợp lệ.')
}
