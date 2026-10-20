import test from 'node:test'
import assert from 'node:assert/strict'
import { getVoucherDiscount, getVoucherError, getVoucherStatus, VoucherStatus } from './voucherValidity.js'

const now = Date.parse('2026-10-20T05:00:00Z')
const voucher = {
  id: 'voucher-1', code: 'GIAM20K', isActive: true,
  discountType: 'FIXED_AMOUNT', discountValue: 20000,
  startDate: '2026-10-03T00:00:00+07:00', endDate: '2026-10-10T00:00:00+07:00',
}

test('the expired voucher in the report is expired and gives no discount despite isActive', () => {
  assert.equal(getVoucherStatus(voucher, now), VoucherStatus.EXPIRED)
  assert.equal(getVoucherDiscount(voucher, 100000, now), 0)
  assert.match(getVoucherError(voucher, 100000, now), /hết hạn/)
})

test('validity starts inclusively and ends exclusively across UTC offsets', () => {
  const timedVoucher = { ...voucher, startDate: '2026-10-20T12:00:00+07:00', endDate: '2026-10-20T12:01:00+07:00' }
  assert.equal(getVoucherStatus(timedVoucher, now - 1), VoucherStatus.SCHEDULED)
  assert.equal(getVoucherStatus(timedVoucher, now), VoucherStatus.ACTIVE)
  assert.equal(getVoucherStatus(timedVoucher, now + 59999), VoucherStatus.ACTIVE)
  assert.equal(getVoucherStatus(timedVoucher, now + 60000), VoucherStatus.EXPIRED)
})

test('a selected cached voucher stops discounting when it expires without refetching', () => {
  const cached = { ...voucher, endDate: new Date(now + 1000).toISOString() }
  assert.equal(getVoucherDiscount(cached, 100000, now), 20000)
  assert.equal(getVoucherDiscount(cached, 100000, now + 1000), 0)
})

test('locked, deleted, scheduled and missing vouchers cannot discount an order', () => {
  for (const invalid of [
    null,
    { ...voucher, endDate: null, isActive: false },
    { ...voucher, endDate: null, isActive: null },
    { ...voucher, endDate: null, isDeleted: true },
    { ...voucher, endDate: null, startDate: new Date(now + 1).toISOString() },
  ]) {
    assert.equal(getVoucherDiscount(invalid, 100000, now), 0)
    assert.ok(getVoucherError(invalid, 100000, now))
  }
})

test('an enabled voucher without date limits works and respects minimum order value', () => {
  const unlimited = { ...voucher, startDate: null, endDate: null, minOrderValue: 100000 }
  assert.equal(getVoucherStatus(unlimited, now), VoucherStatus.ACTIVE)
  assert.equal(getVoucherDiscount(unlimited, 99999, now), 0)
  assert.equal(getVoucherDiscount(unlimited, 100000, now), 20000)
})

test('discounts are capped at the total and percentage rounding matches each checkout', () => {
  const unlimited = { ...voucher, startDate: null, endDate: null }
  assert.equal(getVoucherDiscount(unlimited, 10000, now), 10000)
  const percentage = { ...unlimited, discountType: 'PERCENTAGE', discountValue: 15 }
  assert.equal(getVoucherDiscount(percentage, 99999, now), 14999.85)
  assert.equal(getVoucherDiscount(percentage, 99999, now, true), 15000)
})

test('malformed dates cannot enable a discount', () => {
  assert.equal(getVoucherDiscount({ ...voucher, endDate: 'invalid' }, 100000, now), 0)
  assert.equal(getVoucherDiscount({ ...voucher, endDate: null, startDate: 'invalid' }, 100000, now), 0)
})
