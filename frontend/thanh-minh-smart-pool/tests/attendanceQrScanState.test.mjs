import test from 'node:test'
import assert from 'node:assert/strict'
import { findPreviewAssignment, isQrPreviewActive, normalizeAttendanceAction, reconcileAttendanceScan, shouldShowExpiredQrNotice } from '../src/features/staff/hooks/attendanceQrScanState.js'

const preview = { shiftId: 'shift-1', workDate: '2026-10-19', action: 'CheckIn' }

test('Given exact assignment after check-in When reconciling uncertain scan Then report recorded', () => {
  const assignments = [{ shiftId: 'shift-1', workDate: '2026-10-19', status: 'Present', checkInTime: '2026-10-19T01:00:00Z', checkOutTime: null }]

  assert.equal(reconcileAttendanceScan(assignments, preview), 'recorded')
})

test('Given exact assignment before check-in When reconciling uncertain scan Then report unchanged', () => {
  const assignments = [{ shiftId: 'shift-1', workDate: '2026-10-19', status: 'Scheduled', checkInTime: null, checkOutTime: null }]

  assert.equal(reconcileAttendanceScan(assignments, preview), 'unchanged')
})

test('Given missing or contradictory assignment When reconciling uncertain scan Then report inconsistent', () => {
  const assignments = [{ shiftId: 'shift-2', workDate: '2026-10-19', status: 'Present', checkInTime: '2026-10-19T01:00:00Z', checkOutTime: null }]

  assert.equal(reconcileAttendanceScan(assignments, preview), 'inconsistent')
})

test('Given the own schedule When matching server preview Then use exact shift and work date only', () => {
  const assignments = [
    { id: 'other-date', shiftId: 'shift-1', workDate: '2026-10-18' },
    { id: 'other-shift', shiftId: 'shift-2', workDate: '2026-10-19' },
    { id: 'exact-match', shiftId: 'shift-1', workDate: '2026-10-19' },
  ]

  assert.equal(findPreviewAssignment(assignments, preview)?.id, 'exact-match')
})

test('Given ASP.NET enum JSON When normalizing verified preview Then map numeric actions to their names', () => {
  assert.equal(normalizeAttendanceAction(0), 'CheckIn')
  assert.equal(normalizeAttendanceAction(1), 'CheckOut')
  assert.equal(normalizeAttendanceAction('CheckIn'), 'CheckIn')
  assert.equal(normalizeAttendanceAction('unknown'), null)
})

test('Given future token and session expiries When checking preview freshness Then permit only the valid interval', () => {
  const preview = { tokenExpiresAtUtc: '2026-10-19T10:01:00.000Z', sessionExpiresAtUtc: '2026-10-19T10:15:00.000Z' }
  const now = Date.parse('2026-10-19T10:00:00.000Z')

  assert.equal(isQrPreviewActive(preview, now), true)
  assert.equal(isQrPreviewActive({ ...preview, tokenExpiresAtUtc: '2026-10-19T09:59:59.000Z' }, now), false)
})

test('Given a completed attendance scan When its QR later expires Then keep the completion result instead of an expired-token warning', () => {
  assert.equal(shouldShowExpiredQrNotice('complete', preview, false), false)
  assert.equal(shouldShowExpiredQrNotice('ready', preview, false), true)
})
