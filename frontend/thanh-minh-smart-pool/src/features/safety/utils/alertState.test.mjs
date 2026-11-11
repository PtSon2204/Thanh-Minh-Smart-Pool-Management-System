import test from 'node:test'
import assert from 'node:assert/strict'
import { reconcilePendingAlerts, sortAlerts } from './alertState.js'
import { currentVideoRisks } from './videoFrameState.js'

const now = Date.parse('2026-11-10T03:00:00Z')
const alert = (id, fields = {}) => ({ id, sourceType: 'LIVE', status: 'PENDING', severity: 'WARNING', createdAt: new Date(now - 1000).toISOString(), ...fields })

test('resync removes handled alerts, preserves active older alerts and excludes upload/dismissed/old newcomers', () => {
  const old = alert('old', { createdAt: new Date(now - 180000).toISOString() })
  const pending = [old, alert('fresh'), alert('dismissed'), alert('upload', { sourceType: 'UPLOAD' }), alert('stale', { createdAt: old.createdAt })]
  assert.deepEqual(reconcilePendingAlerts([old, alert('handled')], pending, new Set(['dismissed']), now).map(a => a.id), ['old', 'fresh'])
})

test('newest popup comes first even when an older popup is critical', () => {
  const rows = [alert('early', { createdAt: new Date(now - 101000).toISOString() }), alert('later'), alert('critical', { severity: 'CRITICAL', createdAt: new Date(now - 5000).toISOString() })]
  assert.deepEqual(sortAlerts(rows).map(a => a.id), ['later', 'critical', 'early'])
})

test('video overlays reject missing, future and stale analysis after seeking ahead', () => {
  assert.deepEqual(currentVideoRisks(null, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: null, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 10, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 1, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 4.98, risks: [1] }, 5), [1])
})
