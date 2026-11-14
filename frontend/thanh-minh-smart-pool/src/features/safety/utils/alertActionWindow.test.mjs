import test from 'node:test'
import assert from 'node:assert/strict'
import { canHandleAlert } from './alertActionWindow.js'
import { ALERT_POPUP_LIFETIME_MS } from './popupLifetime.js'

const now = Date.parse('2026-11-13T12:00:00Z')
const alert = (age, extra = {}) => ({ sourceType: 'LIVE', status: 'PENDING', createdAt: new Date(now - age).toISOString(), ...extra })

test('LIVE popup lifetime is ten seconds and actions stop at the exact boundary', () => {
  assert.equal(ALERT_POPUP_LIFETIME_MS, 10000)
  assert.equal(canHandleAlert(alert(0), now), true)
  assert.equal(canHandleAlert(alert(9999), now), true)
  assert.equal(canHandleAlert(alert(10000), now), false)
  assert.equal(canHandleAlert(alert(10001), now), false)
})

test('LIVE actions follow the observation/escalation time rather than a delayed database insert', () => {
  assert.equal(canHandleAlert(alert(0, { observedAt: new Date(now - 11000).toISOString() }), now), false)
  assert.equal(canHandleAlert(alert(30000, { observedAt: new Date(now - 1000).toISOString() }), now), true)
})

test('handled, invalid and future LIVE timestamps cannot offer acknowledgement or cancellation', () => {
  assert.equal(canHandleAlert(alert(0, { status: 'ACKNOWLEDGED' }), now), false)
  assert.equal(canHandleAlert(alert(0, { status: 'CANCELLED' }), now), false)
  assert.equal(canHandleAlert(alert(0, { observedAt: 'invalid' }), now), false)
  assert.equal(canHandleAlert(alert(-1000), now), false)
})

test('UPLOAD review actions are independent of the LIVE ten-second deadline', () => {
  assert.equal(canHandleAlert(alert(600000, { sourceType: 'UPLOAD' }), now), true)
  assert.equal(canHandleAlert(alert(-600000, { sourceType: 'UPLOAD' }), now), true)
  assert.equal(canHandleAlert(alert(0, { sourceType: 'UPLOAD', status: 'CANCELLED' }), now), false)
})
