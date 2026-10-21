import assert from 'node:assert/strict'
import test from 'node:test'
import { claimAttendanceQrStart, reduceAttendanceQrSession } from '../src/features/staff/hooks/attendanceQrState.js'

const fixtureSession = {
  sessionId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  generation: 1,
  qrPayload: 'SMARTPOOL:STAFF:V1:fixture',
  tokenExpiresAtUtc: '2035-01-01T00:01:00.000Z',
  sessionExpiresAtUtc: '2035-01-01T00:15:00.000Z',
}

test('Given an active QR session When a late rotation completes after Stop Then the QR stays hidden', () => {
  const active = reduceAttendanceQrSession(
    { phase: 'idle', session: null, stopBoundary: null },
    { type: 'started', session: fixtureSession },
  )
  const rotated = reduceAttendanceQrSession(active, {
    type: 'rotated',
    session: { ...fixtureSession, generation: 2, qrPayload: 'SMARTPOOL:STAFF:V1:fixture-rotated' },
  })
  const stopping = reduceAttendanceQrSession(rotated, { type: 'stopRequested' })
  const result = reduceAttendanceQrSession(stopping, {
    type: 'rotated',
    session: { ...fixtureSession, generation: 3, qrPayload: 'SMARTPOOL:STAFF:V1:fixture-late' },
  })

  assert.equal(rotated.session.generation, 2)
  assert.equal(result.phase, 'stopping')
  assert.equal(result.session, null)
  assert.equal(result.stopBoundary, fixtureSession.sessionExpiresAtUtc)
})

test('Given a pending QR create When Start is clicked twice Then only the first request is claimed', () => {
  const guard = { current: false }

  assert.equal(claimAttendanceQrStart(guard), true)
  assert.equal(claimAttendanceQrStart(guard), false)
})
