import test from 'node:test'
import assert from 'node:assert/strict'
import { isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState } from './alertState.js'
import { currentVideoRisks } from './videoFrameState.js'

const now = Date.parse('2026-11-10T03:00:00Z')
const alert = (id, fields = {}) => ({ id, sourceType: 'LIVE', status: 'PENDING', severity: 'WARNING', createdAt: new Date(now - 1000).toISOString(), ...fields })

test('resync removes handled alerts, preserves active older alerts and excludes dismissed/stale newcomers', () => {
  const old = alert('old', { createdAt: new Date(now - 180000).toISOString() })
  const pending = [old, alert('fresh'), alert('handled', { status: 'ACKNOWLEDGED' }), alert('dismissed'), alert('stale', { createdAt: old.createdAt })]
  assert.deepEqual(reconcilePendingAlerts([old, alert('handled')], pending, new Set([popupIdentity(alert('dismissed'))]), now).map(a => a.id), ['old', 'fresh'])
})

test('an incomplete pending snapshot cannot close a popup before its own expiry', () => {
  const visible = alert('visible')
  assert.deepEqual(reconcilePendingAlerts([visible], [], new Set(), now), [visible])
})

test('a delayed warning cannot replace a visible critical escalation in HTTP or SignalR', () => {
  const critical = alert('same', { severity: 'CRITICAL', observedAt: new Date(now).toISOString() })
  const warning = alert('same', { observedAt: new Date(now - 500).toISOString() })
  assert.equal(mergeAlertUpdate(critical, warning), critical)
  assert.deepEqual(reconcilePendingAlerts([critical], [warning], new Set([popupIdentity(warning)]), now), [critical])
  assert.equal(mergeAlertUpdate(critical, { ...warning, status: 'CANCELLED' }), critical)
  assert.equal(reconcilePendingAlerts([critical], [{ ...critical, status: 'ACKNOWLEDGED' }], new Set(), now).length, 0)
})

test('newest popup comes first even when an older popup is critical', () => {
  const rows = [alert('early', { createdAt: new Date(now - 101000).toISOString() }), alert('later'), alert('critical', { severity: 'CRITICAL', createdAt: new Date(now - 5000).toISOString() })]
  assert.deepEqual(sortAlerts(rows).map(a => a.id), ['later', 'critical', 'early'])
})

test('late archival saves do not become fresh live popups, but recent escalations do', () => {
  const stale = alert('late-upload', { observedAt: new Date(now - 180000).toISOString() })
  const escalated = alert('escalated', { createdAt: new Date(now - 180000).toISOString(), observedAt: new Date(now - 1000).toISOString() })
  assert.equal(isRecentLiveAlert(stale, now), false)
  assert.equal(isRecentLiveAlert(escalated, now), true)
  assert.deepEqual(reconcilePendingAlerts([], [stale, escalated], new Set(), now).map(a => a.id), ['escalated'])
})

test('camera frames classify a LIVE alert as present, cleared, or unknown without guessing', () => {
  const alert = { id: 'a', sourceType: 'LIVE', cameraId: 'cam-1', trackId: '7', severity: 'WARNING', status: 'PENDING' }
  assert.equal(cameraRiskState({ cameraId: 'CAM-1', state: 'Connected', activeRiskTrackIds: ['7'], displayRisks: [] }, alert), 'present')
  // Tracker re-issues track ids across session resets: a different id with an active
  // risk still means the camera is not clear, so the popup must stay open.
  assert.equal(cameraRiskState({ cameraId: 'cam-1', state: 'Connected', activeRiskTrackIds: ['9'],
    displayRisks: [{ trackId: '9', severity: 'CRITICAL', box: [.1, .1, .3, .3] }] }, alert), 'present')
  assert.equal(cameraRiskState({ cameraId: 'cam-1', state: 'Connected', activeRiskTrackIds: [],
    displayRisks: [{ trackId: '7', severity: 'WARNING', box: [.1, .1, .3, .3] }] }, alert), 'present')
  assert.equal(cameraRiskState({ cameraId: 'cam-1', state: 'Connected', activeRiskTrackIds: [], displayRisks: [] }, alert), 'cleared')
  assert.equal(cameraRiskState({ cameraId: 'cam-1', state: 'Offline', activeRiskTrackIds: [] }, alert), 'unknown')
  assert.equal(cameraRiskState({ cameraId: 'cam-2', state: 'Connected', activeRiskTrackIds: [] }, alert), 'unknown')
})

test('video overlays reject missing, future and stale analysis after seeking ahead', () => {
  assert.deepEqual(currentVideoRisks(null, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: null, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 10, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 1, risks: [1] }, 5), [])
  assert.deepEqual(currentVideoRisks({ analyzedAt: 4.98, risks: [1] }, 5), [1])
})
