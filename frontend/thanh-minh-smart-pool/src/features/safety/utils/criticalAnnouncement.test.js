import test from 'node:test'
import assert from 'node:assert/strict'
import { criticalAnnouncement } from './criticalAnnouncement.js'

const confirmed = { sourceType: 'LIVE', status: 'ACKNOWLEDGED', severity: 'CRITICAL', incidentId: 'incident-a', location: 'Bể test A' }
test('TTS chỉ nhận sự cố LIVE CRITICAL đã xác nhận, dùng khu vực từ server', () => {
  assert.deepEqual(criticalAnnouncement(confirmed), { id: 'incident-a', text: 'Sự cố khẩn cấp xảy ra ở khu vực Bể test A.' })
  for (const changes of [{ status: 'PENDING' }, { status: 'CANCELLED' }, { severity: 'WARNING' },
    { sourceType: 'UPLOAD' }, { incidentId: null }, { location: '' }]) {
    assert.equal(criticalAnnouncement({ ...confirmed, ...changes }), null)
  }
})
