import assert from 'node:assert/strict'
import test from 'node:test'
import {
  createRentalPaymentClock,
  getRentalPaymentSecondsLeft,
  isRentalPaymentFinal,
  isRentalPaymentWaiting,
  readRentalPaymentIntent,
  requiresRentalPaymentReconciliation,
  saveRentalPaymentIntent,
} from './rentalPaymentState.js'

test('rental payment deadline uses the server-time anchor', () => {
  const clock = createRentalPaymentClock({
    id: 'payment-1',
    serverTimeUtc: '2026-10-28T10:00:00.000Z',
    expiresAtUtc: '2026-10-28T10:05:00.000Z',
  }, 1000)

  assert.equal(getRentalPaymentSecondsLeft(clock, 1000), 300)
  assert.equal(getRentalPaymentSecondsLeft(clock, 1500), 300)
  assert.equal(getRentalPaymentSecondsLeft(clock, 301000), 0)
})

test('rental payment statuses preserve polling and reconciliation rules', () => {
  assert.equal(isRentalPaymentWaiting('PENDING'), true)
  assert.equal(isRentalPaymentWaiting('PARTIAL'), true)
  assert.equal(isRentalPaymentWaiting('PAID'), false)
  assert.equal(isRentalPaymentFinal('ISSUED'), true)
  assert.equal(isRentalPaymentFinal('PAID'), false)
  assert.equal(requiresRentalPaymentReconciliation({ status: 'EXPIRED', paidAmount: 10000 }), true)
  assert.equal(requiresRentalPaymentReconciliation({ status: 'CANCELLED', paidAmount: 0, excessAmount: 0 }), false)
  assert.equal(requiresRentalPaymentReconciliation({ status: 'PAID', requiresReconciliation: true }), true)
})

test('uncertain QR creation preserves its frozen request until the server confirms an intent', () => {
  const data = new Map()
  const storage = {
    getItem: (key) => data.get(key) ?? null,
    setItem: (key, value) => data.set(key, value),
    removeItem: (key) => data.delete(key),
  }
  const request = { requestId: 'request-1', customerName: 'Test', items: [{ productId: 'product-1', quantity: 1, expectedUnitPrice: 10000 }] }

  saveRentalPaymentIntent('staff-1', { requestId: request.requestId, paymentId: null, requestPayload: request }, storage)
  assert.deepEqual(readRentalPaymentIntent('staff-1', storage), { requestId: request.requestId, paymentId: null, requestPayload: request })

  saveRentalPaymentIntent('staff-1', { requestId: request.requestId, paymentId: request.requestId, requestPayload: null }, storage)
  assert.deepEqual(readRentalPaymentIntent('staff-1', storage), { requestId: request.requestId, paymentId: request.requestId, requestPayload: null })
})
