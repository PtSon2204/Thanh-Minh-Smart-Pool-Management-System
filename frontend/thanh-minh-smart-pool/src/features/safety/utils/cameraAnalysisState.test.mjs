import test from 'node:test'
import assert from 'node:assert/strict'
import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS, mergeCameraAnalysis } from './cameraAnalysisState.js'

const data = { state: 'Connected', observedAt: '2026-11-10T08:00:00.000Z',
  processedAt: '2026-11-10T08:00:00.080Z', serverTime: '2026-11-10T08:00:00.100Z', receivedAt: 20000, risks: [{ trackId: '1' }] }

test('overlay uses server observation age and local elapsed time, hides boxes after one second', () => {
  assert.equal(cameraAnalysisAge(data, 20050), 150)
  assert.ok(cameraAnalysisAge(data, 20950) >= MAX_OVERLAY_AGE_MS)
  assert.equal(cameraAnalysisAge(null, 20000), Infinity)
  assert.equal(cameraAnalysisAge({ ...data, observedAt: null }, 20000), Infinity)
})

test('late polling responses cannot replace a more recent pushed frame', () => {
  const old = { ...data, processedAt: '2026-11-10T08:00:00.020Z', risks: [] }
  assert.equal(mergeCameraAnalysis(data, old, 20500), data)
})

test('a different tracking session that is not strictly newer must not clobber the active one', () => {
  const current = { ...data, trackingSession: 'session-new', frameId: 'new',
    processedAt: '2026-11-10T08:00:00.500Z', risks: [{ trackId: '1' }] }
  const oldSession = { ...data, trackingSession: 'session-old', frameId: 'old',
    processedAt: '2026-11-10T08:00:00.500Z', risks: [] }
  assert.equal(mergeCameraAnalysis(current, oldSession, 20900), current,
    'an equal/older different session must keep the current session')
})

test('a strictly newer tracking session advances the metadata', () => {
  const current = { ...data, trackingSession: 'session-old', frameId: 'old',
    processedAt: '2026-11-10T08:00:00.100Z' }
  const newSession = { ...data, trackingSession: 'session-new', frameId: 'new',
    processedAt: '2026-11-10T08:00:00.900Z' }
  assert.equal(mergeCameraAnalysis(current, newSession, 20900).trackingSession, 'session-new')
})

test('an empty NotConfigured/Offline HTTP response must not clobber live metadata', () => {
  const live = { ...data, state: 'Connected', trackingSession: 's', processedAt: '2026-11-10T08:00:00.080Z' }
  assert.equal(mergeCameraAnalysis(live, { state: 'NotConfigured', risks: [] }, 21000), live)
  assert.equal(mergeCameraAnalysis(live, { state: 'Offline', risks: [] }, 21000), live,
    'an offline poll after a live push must keep the boxes until the freshness limit, not reset the timeline')
  // Without any metadata yet, the empty response becomes the baseline state.
  assert.equal(mergeCameraAnalysis(null, { state: 'NotConfigured', risks: [] }, 21000).state, 'NotConfigured')
})

test('a new empty AI frame clears the previous warning boxes immediately', () => {
  const next = { ...data, processedAt: '2026-11-10T08:00:00.200Z', risks: [] }
  const merged = mergeCameraAnalysis(data, next, 20100)
  assert.deepEqual(merged.risks, [])
  assert.equal(merged.receivedAt, 20100)
})
