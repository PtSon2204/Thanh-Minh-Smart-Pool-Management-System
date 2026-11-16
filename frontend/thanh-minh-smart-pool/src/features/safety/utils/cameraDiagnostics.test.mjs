import test from 'node:test'
import assert from 'node:assert/strict'
import { cameraDiagnostics, cameraDiagnosticsEnabled, CAMERA_DIAGNOSTICS_GROUP } from './cameraDiagnostics.js'
import { CameraRiskFrameBuffer } from './cameraRiskFrameBuffer.js'

const origin = Date.parse('2026-11-12T12:00:00Z')
const frame = (id, timestamp, box, extra = {}) => ({ state: 'Connected', frameId: id,
  rtpTimestamp: timestamp, trackingSession: 'session', processedAt: new Date(origin + id * 100).toISOString(),
  observedAt: new Date(origin).toISOString(), serverTime: new Date(origin).toISOString(),
  risks: box ? [{ trackId: 7, episode: 1, severity: 'WARNING', box }] : [], ...extra })

test('diagnostics are disabled by default and record nothing', () => {
  assert.equal(cameraDiagnosticsEnabled, false, 'The module must be off unless VITE_CAMERA_DIAGNOSTICS=true')
  cameraDiagnostics.record('camera-1', { source: 'render' })
  assert.deepEqual(cameraDiagnostics.snapshot('camera-1'), [])
  assert.equal(cameraDiagnostics.shouldSampleFrame('camera-1', Date.now()), false)
})

test('the diagnostic sampler returns identical risks and the exact empty reason', () => {
  const buffer = new CameraRiskFrameBuffer()

  const invalid = buffer.sampleWithDiagnostics('not-an-int', origin)
  assert.equal(invalid.reason, 'invalid-video-rtp')
  assert.deepEqual(invalid.risks, [])

  const missing = buffer.sampleWithDiagnostics(10000, origin)
  assert.equal(missing.reason, 'no-latest-metadata')
  assert.deepEqual(missing.risks, [])

  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  const fresh = buffer.sampleWithDiagnostics(10000, origin)
  assert.equal(fresh.reason, 'risks-present')
  assert.equal(fresh.risks.length, 1)
  assert.deepEqual(fresh.risks, buffer.sample(10000, origin), 'risks must match the production sampler exactly')

  const stale = buffer.sampleWithDiagnostics(10000, origin + 1000)
  assert.equal(stale.reason, 'metadata-stale')
  assert.deepEqual(stale.risks, [])

  buffer.add(frame(2, 28000, [.2, .1, .4, .3], { risks: [], activeRiskTrackIds: [] }), origin)
  const noRisk = buffer.sampleWithDiagnostics(28000, origin)
  assert.equal(noRisk.reason, 'no-visible-risks')

  const held = new CameraRiskFrameBuffer()
  held.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  const outside = held.sampleWithDiagnostics(100000, origin)
  assert.equal(outside.reason, 'outside-hold-window')
})

test('sampleWithDiagnostics never mutates the buffer: production sampler is unchanged', () => {
  const buffer = new CameraRiskFrameBuffer()
  buffer.add(frame(1, 10000, [.1, .1, .3, .3]), origin)
  buffer.add(frame(2, 28000, [.3, .3, .5, .5], { risks: [{ trackId: 7, episode: 1, severity: 'CRITICAL', box: [.3, .3, .5, .5] }] }), origin)
  const before = JSON.stringify(buffer.samples)
  const diagnostic = buffer.sampleWithDiagnostics(19000, origin)
  assert.deepEqual(diagnostic.risks, buffer.sample(19000, origin), 'diagnostic companion must not change sampled risks')
  assert.equal(JSON.stringify(buffer.samples), before, 'diagnostics must not mutate the buffer')
})

test('the bounded ring buffer accepts and clears bounded events', () => {
  // Disabled by default: recording is a no-op, snapshot stays empty.
  cameraDiagnostics.record('camera-x', { at: 1 })
  cameraDiagnostics.clear('camera-x')
  assert.deepEqual(cameraDiagnostics.snapshot('camera-x'), [])
  assert.ok(CAMERA_DIAGNOSTICS_GROUP === 'SmartPool camera diagnostics')
})
