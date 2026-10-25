import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatCurrency,
  getInvoicePageState,
  getMethodLabel,
  getStatusLabel,
  getTicketCategoryLabel,
  getTicketStatusLabel,
  numberToVietnameseWords,
} from './invoicePrintUtils.js'

test('UTCID01 (N) - Invoice data đang tải -> Trạng thái loading', () => {
  assert.equal(getInvoicePageState({ data: null, isLoading: true, error: null }), 'loading')
})

test('UTCID02 (A) - API có error -> Trạng thái error', () => {
  assert.equal(getInvoicePageState({ data: null, isLoading: false, error: new Error('failed') }), 'error')
})

test('UTCID03 (A) - API không lỗi nhưng không có data -> Trạng thái error', () => {
  assert.equal(getInvoicePageState({ data: null, isLoading: false, error: null }), 'error')
})

test('UTCID04 (N) - Có invoice data và không loading -> Trạng thái ready', () => {
  assert.equal(getInvoicePageState({ data: { paymentId: 'id' }, isLoading: false, error: null }), 'ready')
})

test('UTCID05 (N) - formatCurrency với số tiền -> Định dạng theo vi-VN và thêm ₫', () => {
  assert.equal(formatCurrency(1234567), '1.234.567 ₫')
})

test('UTCID06 (B) - formatCurrency với null hoặc undefined -> 0 ₫', () => {
  assert.equal(formatCurrency(null), '0 ₫')
  assert.equal(formatCurrency(undefined), '0 ₫')
})

test('UTCID07 (N) - getStatusLabel COMPLETED -> Đã thanh toán', () => {
  assert.deepEqual(getStatusLabel('COMPLETED'), { label: 'Đã thanh toán', cls: 'status-completed' })
})

test('UTCID08 (A) - getStatusLabel FAILED -> Thất bại', () => {
  assert.deepEqual(getStatusLabel('FAILED'), { label: 'Thất bại', cls: 'status-failed' })
})

test('UTCID09 (N) - getStatusLabel trạng thái khác -> Chờ xử lý', () => {
  assert.deepEqual(getStatusLabel('PENDING'), { label: 'Chờ xử lý', cls: 'status-pending' })
})

test('UTCID10 (N) - getMethodLabel TRANSFER, BANK_TRANSFER và CASH -> Nhãn theo code', () => {
  assert.equal(getMethodLabel('TRANSFER'), 'Chuyển khoản ngân hàng')
  assert.equal(getMethodLabel('BANK_TRANSFER'), 'Chuyển khoản ngân hàng')
  assert.equal(getMethodLabel('CASH'), 'Tiền mặt')
})

test('UTCID11 (B) - getMethodLabel null hoặc chuỗi rỗng -> ---', () => {
  assert.equal(getMethodLabel(null), '---')
  assert.equal(getMethodLabel(''), '---')
})

test('UTCID12 (N) - getTicketCategoryLabel VE_THANG và VE_LUOT -> Nhãn tiếng Việt', () => {
  assert.equal(getTicketCategoryLabel('VE_THANG'), 'Vé tháng')
  assert.equal(getTicketCategoryLabel('VE_LUOT'), 'Vé lượt')
})

test('UTCID13 (N) - getTicketStatusLabel ACTIVE, EXPIRED, USED -> Nhãn theo code', () => {
  assert.equal(getTicketStatusLabel('ACTIVE'), 'Còn hiệu lực')
  assert.equal(getTicketStatusLabel('EXPIRED'), 'Đã hết hạn')
  assert.equal(getTicketStatusLabel('USED'), 'Đã sử dụng')
})

test('UTCID14 (B) - numberToVietnameseWords với 0 hoặc NaN -> Không', () => {
  assert.equal(numberToVietnameseWords(0), 'Không')
  assert.equal(numberToVietnameseWords(Number.NaN), 'Không')
})

test('UTCID15 (N) - numberToVietnameseWords làm tròn số tiền và đọc số dương', () => {
  assert.equal(numberToVietnameseWords(125000), 'Một trăm hai mươi lăm nghìn')
  assert.equal(numberToVietnameseWords(1.6), 'Hai')
})
