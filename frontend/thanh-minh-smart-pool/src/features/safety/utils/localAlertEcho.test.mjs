import test from 'node:test'
import assert from 'node:assert/strict'
import { clearLocalSafetyAlerts, isLocalSafetyAlertEcho, rememberLocalSafetyAlert } from './localAlertEcho.js'

test('localAlertEcho suppresses matching clientRequestId within TTL and ignores other ids', () => {
  clearLocalSafetyAlerts()
  const now = 1_000_000
  rememberLocalSafetyAlert('req-1', now)

  assert.equal(isLocalSafetyAlertEcho({ clientRequestId: 'req-1' }, now + 5_000), true)
  assert.equal(isLocalSafetyAlertEcho({ clientRequestId: 'req-2' }, now + 5_000), false)
  assert.equal(isLocalSafetyAlertEcho({}, now + 5_000), false)
  assert.equal(isLocalSafetyAlertEcho(null, now + 5_000), false)
})

test('localAlertEcho expires remembered ids after 60 seconds', () => {
  clearLocalSafetyAlerts()
  const now = 2_000_000
  rememberLocalSafetyAlert('req-expire', now)

  assert.equal(isLocalSafetyAlertEcho({ clientRequestId: 'req-expire' }, now + 59_999), true)
  assert.equal(isLocalSafetyAlertEcho({ clientRequestId: 'req-expire' }, now + 60_000), false)
})
