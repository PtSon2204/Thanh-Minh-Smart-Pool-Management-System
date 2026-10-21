import test from 'node:test'
import assert from 'node:assert/strict'
import { formatVietnamLocalDateTime, serializeVietnamLocalDateTime, isAfterVietnamLocalDateTime } from '../src/features/staff/schedule/attendanceCorrectionForm.js'

test('Given a UTC timestamp When displaying a correction field Then show Vietnam local time', () => {
  assert.equal(formatVietnamLocalDateTime('2026-10-19T01:00:00.000Z'), '2026-10-19T08:00')
})

test('Given a Vietnam local date and time When serializing correction input Then include the explicit offset', () => {
  assert.equal(serializeVietnamLocalDateTime('2026-10-19T08:30'), '2026-10-19T08:30:00+07:00')
})

test('Given check-in and check-out local values When checking order Then require checkout after check-in', () => {
  assert.equal(isAfterVietnamLocalDateTime('2026-10-19T12:00', '2026-10-19T08:00'), true)
  assert.equal(isAfterVietnamLocalDateTime('2026-10-19T08:00', '2026-10-19T08:00'), false)
  assert.equal(isAfterVietnamLocalDateTime(null, '2026-10-19T08:00'), false)
})
