// Ghi nhận accept/reject của metadata tại biên HTTP polling và SignalR.
// Chạy hook trong vm context (không React thật) với cameraDiagnostics + cache
// giả, để kiểm tra nội dung sự kiện ghi nhận, không phụ thuộc flag build.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { CameraRiskFrameBuffer } from '../utils/cameraRiskFrameBuffer.js'
import { cameraAnalysisAge } from '../utils/cameraAnalysisState.js'

async function fixture() {
  const diagnostics = new Map()
  const cameraDiagnostics = {
    record(cameraId, event) { const list = diagnostics.get(cameraId) || []; list.push(event); diagnostics.set(cameraId, list) },
    snapshot(cameraId) { return diagnostics.get(cameraId) || [] },
    clear() {},
  }
  const cache = { current: null }
  let subscribe
  const context = {
    Date,
    CameraRiskFrameBuffer,
    cameraDiagnostics,
    useState: initial => [typeof initial === 'function' ? initial() : initial, () => {}],
    useMemo: fn => fn(), useCallback: fn => fn,
    // Chạy effect thật để subscription SignalR được đăng ký (mục tiêu của test).
    useEffect: fn => fn(),
    useQuery: options => { context.queryOptions = options; return { data: null, isError: false } },
    useQueryClient: () => ({
      getQueryData: () => cache.current,
      setQueryData: (_key, updater) => { cache.current = updater(cache.current) },
    }),
    useAuthStore: selector => selector({ session: { id: 'viewer', accessToken: 'fixture' } }),
    signalRIdentity: () => 'viewer|fixture',
    monitoringService: { analysis: async () => ({ state: 'NotConfigured' }) },
    subscribeCameraAnalysis: (_cameraId, listener) => { subscribe = payload => listener(payload); return () => {} },
    setInterval: () => 0, clearInterval() {},
    cameraAnalysisAge, MAX_OVERLAY_AGE_MS: 1000, MONITORING_STALE_MS: 10000,
    mergeCameraAnalysis: undefined, // filled below
  }
  context.mergeCameraAnalysis = await import('../utils/cameraAnalysisState.js').then(m => m.mergeCameraAnalysis)
  context.metadataRejectionReason = await import('../utils/cameraAnalysisState.js').then(m => m.metadataRejectionReason)
  vm.createContext(context)
  vm.runInContext(readFileSync(new URL('./useCameraRisks.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replace('export function ', 'function '),
    context, { filename: 'useCameraRisks.js' })
  return {
    context, diagnostics, cache,
    invoke: () => context.useCameraRisks('cam-1', true),
    get queryFn() { return context.queryOptions?.queryFn },
    push: payload => { subscribe?.(payload); return new Promise(resolve => setImmediate(resolve)) },
  }
}

const origin = Date.parse('2026-11-12T12:00:00Z')
const frame = (id, extra = {}) => ({ state: 'Connected', cameraId: 'CAM-1', frameId: id,
  trackingSession: 'session', rtpTimestamp: Number(id) * 90000,
  processedAt: new Date(origin + Number(id) * 100).toISOString(),
  observedAt: new Date(origin).toISOString(), serverTime: new Date(origin).toISOString(),
  risks: [], activeRiskTrackIds: [], ...extra })

test('a SignalR frame is recorded as accepted with its payload identity', async () => {
  const f = await fixture()
  f.invoke()
  await f.push({ ...frame(1), processedAt: new Date(origin).toISOString() })
  const events = f.diagnostics.get('cam-1').filter(e => e.source === 'signalr')
  assert.equal(events.length, 1)
  assert.equal(events[0].frameId, 1, 'the recorded event carries the pushed payload identity')
  assert.equal(events[0].rejected, false)
})

test('an old tracking session push is recorded as rejected with the reason', async () => {
  const f = await fixture()
  f.invoke()
  await f.push(frame(5))
  await f.push({ ...frame(1), trackingSession: 'older-session',
    observedAt: new Date(origin - 1000).toISOString() })
  const events = f.diagnostics.get('cam-1').filter(e => e.source === 'signalr')
  assert.equal(events.length, 2)
  assert.equal(events[0].rejected, false)
  assert.equal(events[1].rejected, true)
  assert.equal(events[1].reason, 'older-session')
})

test('a stale HTTP response that loses the race is recorded as rejected', async () => {
  const f = await fixture()
  f.invoke()
  f.cache.current = { ...frame(2) }
  const merged = await f.queryFn({ signal: null })
  assert.equal(merged.frameId, 2, 'the late HTTP response must keep the pushed frame')
  const events = f.diagnostics.get('cam-1').filter(e => e.source === 'http-poll')
  assert.equal(events.length, 1)
  assert.equal(events[0].rejected, true)
})
