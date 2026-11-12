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

test('a new empty AI frame clears the previous warning boxes immediately', () => {
  const next = { ...data, processedAt: '2026-11-10T08:00:00.200Z', risks: [] }
  const merged = mergeCameraAnalysis(data, next, 20100)
  assert.deepEqual(merged.risks, [])
  assert.equal(merged.receivedAt, 20100)
})
