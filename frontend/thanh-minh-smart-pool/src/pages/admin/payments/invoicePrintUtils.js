export const formatCurrency = (val) => {
  if (val === null || val === undefined) return '0 ₫'
  return new Intl.NumberFormat('vi-VN').format(val) + ' ₫'
}

export const getStatusLabel = (status) => {
  if (status === 'COMPLETED') return { label: 'Đã thanh toán', cls: 'status-completed' }
  if (status === 'FAILED') return { label: 'Thất bại', cls: 'status-failed' }
  return { label: 'Chờ xử lý', cls: 'status-pending' }
}

export const getMethodLabel = (method) => {
  if (method === 'BANK_TRANSFER' || method === 'TRANSFER') return 'Chuyển khoản ngân hàng'
  if (method === 'CASH') return 'Tiền mặt'
  return method || '---'
}

export const getTicketCategoryLabel = (cat) => {
  if (cat === 'VE_THANG') return 'Vé tháng'
  if (cat === 'VE_LUOT') return 'Vé lượt'
  return cat || ''
}

export const getTicketStatusLabel = (status) => {
  if (status === 'ACTIVE') return 'Còn hiệu lực'
  if (status === 'EXPIRED') return 'Đã hết hạn'
  if (status === 'USED') return 'Đã sử dụng'
  return status || '---'
}

export const getInvoicePageState = ({ data, isLoading, error }) => {
  if (isLoading) return 'loading'
  if (error || !data) return 'error'
  return 'ready'
}

export function numberToVietnameseWords(num) {
  if (!num || isNaN(num)) return 'Không'
  const ones = ['', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín']
  const teens = [
    'mười', 'mười một', 'mười hai', 'mười ba', 'mười bốn', 'mười lăm',
    'mười sáu', 'mười bảy', 'mười tám', 'mười chín',
  ]
  const tens = [
    '', '', 'hai mươi', 'ba mươi', 'bốn mươi', 'năm mươi',
    'sáu mươi', 'bảy mươi', 'tám mươi', 'chín mươi',
  ]

  const toWords = (n) => {
    if (n === 0) return ''
    if (n < 10) return ones[n]
    if (n < 20) return teens[n - 10]
    if (n < 100) {
      const t = Math.floor(n / 10)
      const o = n % 10
      return tens[t] + (o ? ' ' + (o === 5 ? 'lăm' : ones[o]) : '')
    }
    if (n < 1000) {
      const h = Math.floor(n / 100)
      const rem = n % 100
      return ones[h] + ' trăm' + (rem ? ' ' + toWords(rem) : '')
    }
    if (n < 1_000_000) {
      const th = Math.floor(n / 1000)
      const rem = n % 1000
      return toWords(th) + ' nghìn' + (rem ? ' ' + toWords(rem) : '')
    }
    if (n < 1_000_000_000) {
      const m = Math.floor(n / 1_000_000)
      const rem = n % 1_000_000
      return toWords(m) + ' triệu' + (rem ? ' ' + toWords(rem) : '')
    }
    const b = Math.floor(n / 1_000_000_000)
    const rem = n % 1_000_000_000
    return toWords(b) + ' tỷ' + (rem ? ' ' + toWords(rem) : '')
  }

  const rounded = Math.round(num)
  const words = toWords(rounded)
  return words.charAt(0).toUpperCase() + words.slice(1)
}
