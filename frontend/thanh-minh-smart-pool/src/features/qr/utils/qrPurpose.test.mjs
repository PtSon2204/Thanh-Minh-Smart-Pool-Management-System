import assert from 'node:assert/strict'
import test from 'node:test'
import { QR_PURPOSE, STAFF_QR_PREFIX, parseQrPayload } from './qrPurpose.js'

test('ticket mode preserves legacy ticket codes and rejects staff tokens', () => {
  const ticketCode = '  123456  '

  assert.deepEqual(parseQrPayload(ticketCode, QR_PURPOSE.ticket), {
    accepted: true,
    payload: ticketCode,
  })
  assert.equal(
    parseQrPayload(`${STAFF_QR_PREFIX}opaque-token`, QR_PURPOSE.ticket).accepted,
    false,
  )
})

test('staff mode accepts only the versioned staff prefix', () => {
  assert.deepEqual(
    parseQrPayload(`${STAFF_QR_PREFIX}opaque-token`, QR_PURPOSE.staff),
    { accepted: true, payload: `${STAFF_QR_PREFIX}opaque-token` },
  )
  assert.equal(parseQrPayload('123456', QR_PURPOSE.staff).accepted, false)
})

test('parser rejects malformed and overlength payloads', () => {
  assert.equal(parseQrPayload(' ', QR_PURPOSE.ticket).accepted, false)
  assert.equal(parseQrPayload('x'.repeat(256), QR_PURPOSE.ticket).accepted, false)
  assert.equal(parseQrPayload(`${STAFF_QR_PREFIX}${'x'.repeat(512)}`, QR_PURPOSE.staff).accepted, false)
})
