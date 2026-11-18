import test from 'node:test'
import assert from 'node:assert/strict'
import { criticalAnnouncement } from './criticalAnnouncement.js'

const confirmed = { status: 'ACKNOWLEDGED', severity: 'CRITICAL', incidentId: 'incident-a', location: 'Bể test A' }

test('TTS đọc vị trí cho cả sự cố CRITICAL và WARNING đã xác nhận, bỏ qua các trạng thái khác', () => {
  assert.deepEqual(criticalAnnouncement(confirmed), { id: 'incident-a', text: 'Sự cố khẩn cấp xảy ra ở khu vực Bể test A.' })
  assert.deepEqual(
    criticalAnnouncement({ ...confirmed, severity: 'WARNING' }),
    { id: 'incident-a', text: 'Cảnh báo an toàn tại khu vực Bể test A.' },
  )
  for (const changes of [{ status: 'PENDING' }, { status: 'CANCELLED' }, { severity: 'INFO' },
    { incidentId: null }, { location: '' }]) {
    assert.equal(criticalAnnouncement({ ...confirmed, ...changes }), null)
  }
})
