export const VoucherStatus = {
  ACTIVE: 'ACTIVE',
  EXPIRED: 'EXPIRED',
  SCHEDULED: 'SCHEDULED',
  LOCKED: 'LOCKED',
}

// Dates from the API represent instants; the end of the validity interval is exclusive.
export function getVoucherStatus(voucher, now = Date.now()) {
  if (!voucher || voucher.isDeleted === true) return VoucherStatus.LOCKED
  if (voucher.endDate && !(Date.parse(voucher.endDate) > now)) return VoucherStatus.EXPIRED
  if (voucher.isActive !== true) return VoucherStatus.LOCKED
  if (voucher.startDate && !(Date.parse(voucher.startDate) <= now)) return VoucherStatus.SCHEDULED
  return VoucherStatus.ACTIVE
}

export function getVoucherError(voucher, totalAmount, now = Date.now()) {
  if (!voucher) return 'Mã giảm giá không còn khả dụng. Vui lòng chọn lại.'
  const status = getVoucherStatus(voucher, now)
  if (status === VoucherStatus.EXPIRED) return 'Mã giảm giá đã hết hạn.'
  if (status === VoucherStatus.SCHEDULED) return 'Mã giảm giá chưa đến thời gian sử dụng.'
  if (status !== VoucherStatus.ACTIVE) return 'Mã giảm giá đã bị khóa hoặc không còn khả dụng.'
  if (voucher.minOrderValue && totalAmount < voucher.minOrderValue) {
    return `Đơn hàng tối thiểu ${new Intl.NumberFormat('vi-VN').format(voucher.minOrderValue)}đ để dùng mã này.`
  }
  return null
}

export function getVoucherDiscount(voucher, totalAmount, now = Date.now(), roundPercentage = false) {
  if (getVoucherError(voucher, totalAmount, now)) return 0
  let discount = voucher.discountType === 'PERCENTAGE'
    ? totalAmount * voucher.discountValue / 100
    : voucher.discountValue
  if (roundPercentage && voucher.discountType === 'PERCENTAGE') discount = Math.round(discount)
  return Math.max(0, Math.min(discount, totalAmount))
}
