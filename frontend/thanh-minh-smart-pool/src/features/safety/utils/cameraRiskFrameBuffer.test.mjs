import test from 'node:test'
import assert from 'node:assert/strict'
import { CameraRiskFrameBuffer } from './cameraRiskFrameBuffer.js'

const origin = Date.parse('2026-11-12T12:00:00Z')
const frame = (id, timestamp, box, extra = {}) => ({ state: 'Connected', frameId: id,
  rtpTimestamp: timestamp, trackingSession: 'session', processedAt: new Date(origin + id * 100).toISOString(),
  observedAt: new Date(origin).toISOString(), serverTime: new Date(origin).toISOString(),
  risks: [{ trackId: 7, episode: 1, severity: 'WARNING', box }], ...extra })

test('a newly opened viewer seeds a latched warning from current display geometry even without a new risk decision', () => {
  const buffer = new CameraRiskFrameBuffer()
  const box = [.2, .1, .4, .3]
  buffer.add(frame(1, 10000, null, { risks: [], activeRiskTrackIds: [7],
    displayRisks: [{ trackId: 7, episode: 1, severity: 'CRITICAL', box }],
    detections: [{ trackId: 7, confidence: .4, box }],
  }), origin)
  const visible = buffer.sample(10000, origin)
  assert.equal(visible.length, 1)
  assert.equal(visible[0].severity, 'CRITICAL')
  assert.deepEqual(visible[0].box, box)
  assert.deepEqual(buffer.latest.risks, [], 'Display boxes must not become saved risk observations')
})

test('interpolates coordinates using the video clock while keeping past severity', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 28000, [.3, .3, .5, .5], { risks: [{ trackId: 7, episode: 1, severity: 'CRITICAL', box: [.3, .3, .5, .5] }] }), origin)
  const [risk] = buffer.sample(19000, origin)
  assert.equal(risk.severity, 'WARNING')
  assert.deepEqual(risk.box, [.2, .2, .4, .4])
  assert.equal(buffer.sample(28000, origin)[0].severity, 'CRITICAL')
})

test('hides future-only, stale and unbounded extrapolated boxes', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  assert.deepEqual(buffer.sample(9999, origin), [])
  assert.equal(buffer.sample(19000, origin).length, 1)
  assert.equal(buffer.sample(28000, origin).length, 1)
  assert.equal(buffer.sample(91000, origin).length, 1)
  assert.deepEqual(buffer.sample(91001, origin), [])
  assert.deepEqual(buffer.sample(10000, origin + 1000), [])
})

test('a bracketed moving track stays aligned across a sparse AI interval beyond the prediction cutoff', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 82000, [.5, .1, .7, .3]), origin)
  const [risk] = buffer.sample(68500, origin) // 650 ms after past, 150 ms before future
  assert.ok(risk, 'A valid interpolation segment must not blink at the extrapolation timeout')
  assert.ok(Math.abs(risk.box[0] - .425) < 1e-9)
  assert.equal(buffer.sample(82000, origin)[0].box[0], .5)
})

test('a future frame without the same track does not extend an old box beyond its hold timeout', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 110000, null, { risks: [] }), origin)
  assert.deepEqual(buffer.sample(100000, origin), [])
})

test('draws continuously at 25 FPS between delayed 5 Hz AI updates', () => {
  const buffer = new CameraRiskFrameBuffer()
  let missing = 0
  for (let ms = 0; ms < 2000; ms += 40) {
    // Latest observation lags the displayed video by 160–320 ms, with one push delayed.
    if (ms % 200 === 0 && ms !== 800) {
      const observed = ms - 160
      buffer.add(frame(ms + 1, 100000 + observed * 90, [.2, .2, .4, .4], {
        observedAt: new Date(origin + observed).toISOString(), serverTime: new Date(origin + ms).toISOString(),
      }), origin + ms)
    }
    if (buffer.sample(100000 + ms * 90, origin + ms).length === 0) missing++
  }
  assert.equal(missing, 0, 'Metadata gaps must not erase a current warning between video frames')
})

test('predicts centre motion briefly without changing size, ID or severity', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .2, .3, .4]), origin)
  buffer.add(frame(2, 28000, [.2, .2, .4, .4]), origin)
  const [risk] = buffer.sample(46000, origin)
  assert.equal(risk.trackId, 7)
  assert.equal(risk.severity, 'WARNING')
  assert.ok(Math.abs(risk.box[0] - .3) < 1e-9)
  assert.ok(Math.abs(risk.box[2] - .5) < 1e-9)
  assert.deepEqual(buffer.sample(64000, origin)[0].box, risk.box, 'Motion prediction stops after 200 ms')
})

test('a real no-risk observation removes the box immediately', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 28000, null, { risks: [] }), origin)
  assert.deepEqual(buffer.sample(28000, origin), [])
})

test('a brief confidence dip keeps the explicitly active warning at the measured track position', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 19000, null, { risks: [], activeRiskTrackIds: [7],
    detections: [{ trackId: 7, confidence: .4, box: [.2, .1, .4, .3] }] }), origin)
  const [risk] = buffer.sample(19000, origin)
  assert.equal(risk.severity, 'WARNING')
  assert.deepEqual(risk.box, [.2, .1, .4, .3])
  assert.deepEqual(buffer.latest.risks, [], 'Observed risks used for persistence must stay unchanged')
})

test('fresh track geometry keeps a latched risk visible beyond the 32-frame history without reviving missing people', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  for (let index = 2; index <= 60; index++) {
    const ms = index * 100
    buffer.add(frame(index, 10000 + ms * 90, null, { risks: [], activeRiskTrackIds: [7],
      detections: [{ trackId: 7, confidence: .4, box: [.2, .1, .4, .3] }],
      observedAt: new Date(origin + ms).toISOString(), serverTime: new Date(origin + ms).toISOString(),
    }), origin + ms)
    assert.equal(buffer.sample(10000 + ms * 90, origin + ms).length, 1)
  }
  const end = 10000 + 6000 * 90
  buffer.add(frame(61, end + 9000, null, { risks: [], activeRiskTrackIds: [7],
    observedAt: new Date(origin + 6100).toISOString(), serverTime: new Date(origin + 6100).toISOString(),
  }), origin + 6100)
  assert.equal(buffer.sample(end + 45000, origin + 6300).length, 1)
  assert.equal(buffer.sample(end + 45001, origin + 6301).length, 0)
  buffer.add(frame(62, end + 36000, null, { risks: [], activeRiskTrackIds: [],
    detections: [{ trackId: 7, box: [.2, .1, .4, .3] }],
  }), origin + 6400)
  assert.equal(buffer.latched.size, 0, 'Confirmed recovery must remove the display latch')
})

test('carried boxes expire from the last real risk and never renew themselves', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 19000, null, { risks: [], activeRiskTrackIds: [7] }), origin)
  buffer.add(frame(3, 28000, null, { risks: [], activeRiskTrackIds: [7] }), origin)
  assert.equal(buffer.sample(55000, origin).length, 1)
  assert.deepEqual(buffer.sample(55001, origin), [])
})

test('confirmed recovery or a changed track removes the old box even within the continuity window', () => {
  for (const activeRiskTrackIds of [[], [8]]) {
    const buffer = new CameraRiskFrameBuffer()
    buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
    buffer.add(frame(2, 19000, null, { risks: [], activeRiskTrackIds }), origin)
    assert.deepEqual(buffer.sample(19000, origin), [])
  }
})

test('multiple active tracks bridge one missing observation without duplicating boxes', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3], { risks: [
    { trackId: 7, episode: 1, severity: 'CRITICAL', box: [.1, .1, .3, .3] },
    { trackId: 8, episode: 2, severity: 'WARNING', box: [.5, .5, .7, .7] },
  ] }), origin)
  buffer.add(frame(2, 19000, [.2, .1, .4, .3], { activeRiskTrackIds: [7, 8] }), origin)
  const risks = buffer.sample(19000, origin)
  assert.deepEqual(risks.map(r => r.trackId).sort(), [7, 8])
  buffer.add({ state: 'CameraOffline' }, origin)
  assert.deepEqual(buffer.sample(19000, origin), [])
})

test('handles RTP wrap and never interpolates across a tracking session', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 0xfffff000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 0x1000, [.3, .3, .5, .5]), origin)
  assert.deepEqual(buffer.sample(0, origin)[0].box, [.2, .2, .4, .4])
  buffer.add(frame(3, 0x2000, [.3, .3, .5, .5], { trackingSession: 'new-session' }), origin)
  assert.deepEqual(buffer.sample(0x1000, origin), [])
})

test('ignores old observations, bounds memory and clears disconnected state', () => {
  const buffer = new CameraRiskFrameBuffer()
  for (let id = 1; id <= 50; id++) buffer.add(frame(id, id * 3600, [.1, .1, .3, .3]), origin)
  assert.equal(buffer.samples.length, 32)
  buffer.add(frame(2, 7200, [.1, .1, .3, .3]), origin)
  assert.equal(buffer.latest.frameId, 50)
  buffer.add({ state: 'Disconnected' }, origin)
  assert.deepEqual(buffer.sample(50 * 3600, origin), [])
})
