import assert from 'node:assert/strict'
import test from 'node:test'
import { initialAttendanceQrState, reduceAttendanceQrSession } from './attendanceQrState.js'
import { isQrPreviewActive, reconcileAttendanceScan, shouldShowExpiredQrNotice } from './attendanceQrScanState.js'

test('stale or foreign session rotation leaves the active session unchanged', () => {
  const session = { sessionId: 'session-a', generation: 2 }
  const state = { phase: 'active', session, stopBoundary: null }

  assert.equal(reduceAttendanceQrSession(state, { type: 'rotated', session: { ...session, generation: 2 } }), state)
  assert.equal(reduceAttendanceQrSession(state, { type: 'rotated', session: { sessionId: 'session-b', generation: 3 } }), state)
})

test('stop hides an active session and failed stop keeps the uncertain state', () => {
  const active = {
    phase: 'active',
    session: { sessionId: 'session-a', generation: 1, sessionExpiresAtUtc: '2026-10-05T10:15:00Z' },
    stopBoundary: null,
  }

  const stopping = reduceAttendanceQrSession(active, { type: 'stopRequested' })
  assert.equal(stopping.phase, 'stopping')
  assert.equal(stopping.session, null)
  assert.equal(stopping.stopBoundary, active.session.sessionExpiresAtUtc)
  const uncertain = reduceAttendanceQrSession(stopping, { type: 'stopFailed', stopBoundary: active.session.sessionExpiresAtUtc })
  assert.equal(uncertain.phase, 'stopUncertain')
  assert.equal(uncertain.session, null)
  assert.equal(reduceAttendanceQrSession(uncertain, { type: 'stopped' }), initialAttendanceQrState)
})

test('expired QR preview is inactive and warns until the scan completes', () => {
  const preview = { tokenExpiresAtUtc: '2026-10-05T10:00:00Z', sessionExpiresAtUtc: '2026-10-05T10:15:00Z' }

  assert.equal(isQrPreviewActive(preview, Date.parse('2026-10-05T10:00:00Z')), false)
  assert.equal(shouldShowExpiredQrNotice('preview', preview, false), true)
  assert.equal(shouldShowExpiredQrNotice('complete', preview, false), false)
})

test('scan reconciliation distinguishes recorded, unchanged, and inconsistent check-in state', () => {
  const preview = { shiftId: 'shift-a', workDate: '2026-10-05', action: 'CheckIn' }
  const assignment = { ...preview, status: 'Present', checkInTime: '2026-10-05T02:00:00Z', checkOutTime: null }

  assert.equal(reconcileAttendanceScan([assignment], preview), 'recorded')
  assert.equal(reconcileAttendanceScan([{ ...assignment, status: 'Scheduled', checkInTime: null }], preview), 'unchanged')
  assert.equal(reconcileAttendanceScan([], preview), 'inconsistent')
})
