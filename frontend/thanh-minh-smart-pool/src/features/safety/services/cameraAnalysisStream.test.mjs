// Kiểm tra định tuyến sự kiện camera_analysis, kết quả subscribe (thành công
// lẫn thất bại) và tự đăng ký lại nhóm camera sau reconnect. Kết quả được ghi
// qua cameraDiagnostics (ring buffer bounded, tắt mặc định); fixture dùng object
// giả trong bộ nhớ để kiểm tra nội dung ghi nhận không phụ thuộc flag build.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'

function fixture() {
  const diagnostics = new Map()
  const cameraDiagnostics = {
    record(cameraId, event) { const list = diagnostics.get(cameraId) || []; list.push(event); diagnostics.set(cameraId, list) },
    snapshot(cameraId) { return diagnostics.get(cameraId) || [] },
    clear(cameraId) { diagnostics.delete(cameraId) },
  }
  const invokes = []
  const handlers = new Map()
  const stateListeners = []
  const connection = {
    state: 'Connected',
    on(event, fn) { handlers.set(event, fn) },
    invoke(name, ...args) {
      invokes.push({ name, args })
      return name === 'UnsubscribeCameraAnalysis'
        ? Promise.reject(new Error('no connection'))
        : Promise.resolve()
    },
  }
  const context = {
    connection,
    getSignalRConnection: () => connection,
    startSignalRConnection: async () => true,
    onSignalRStateChange: fn => { stateListeners.push(fn); return () => {} },
    cameraDiagnostics,
  }
  const source = readFileSync(new URL('./cameraAnalysisStream.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replaceAll('export function ', 'function ')
  vm.createContext(context)
  vm.runInContext(source, context, { filename: 'cameraAnalysisStream.js' })
  return {
    context, diagnostics, invokes, stateListeners,
    fire: state => stateListeners.forEach(fn => fn(state)),
    push: payload => handlers.get('camera_analysis')(payload),
    flush: () => new Promise(resolve => setImmediate(resolve)),
  }
}

test('a successful camera group subscription is recorded with its camera', async () => {
  const f = fixture()
  f.context.subscribeCameraAnalysis('CAM-1', () => {})
  await f.flush()
  assert.deepEqual(f.invokes, [{ name: 'SubscribeCameraAnalysis', args: ['cam-1'] }])
  assert.ok(f.diagnostics.get('cam-1')?.some(e => e.source === 'signalr-subscribe' && e.ok === true),
    'the successful join must be diagnosable')
})

test('a failed subscription is recorded with the failure reason, not swallowed silently', async () => {
  const f = fixture()
  // Module copy riêng, hub từ chối join (ví dụ: không đủ quyền).
  const diagnostics = new Map()
  const cameraDiagnostics = {
    record(cameraId, event) { const list = diagnostics.get(cameraId) || []; list.push(event); diagnostics.set(cameraId, list) },
    snapshot(cameraId) { return diagnostics.get(cameraId) || [] },
    clear(cameraId) { diagnostics.delete(cameraId) },
  }
  const failedConnection = { state: 'Connected', on() {},
    invoke: async () => { throw new Error('Không có quyền xem phân tích camera.') } }
  const failed = {
    getSignalRConnection: () => failedConnection,
    startSignalRConnection: async () => true,
    onSignalRStateChange: fn => { return () => {} },
    cameraDiagnostics,
  }
  vm.createContext(failed)
  const source = readFileSync(new URL('./cameraAnalysisStream.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replaceAll('export function ', 'function ')
  vm.runInContext(source, failed, { filename: 'cameraAnalysisStream.js' })
  failed.subscribeCameraAnalysis('CAM-2', () => {})
  await f.flush()
  assert.ok(failed.cameraDiagnostics.snapshot('cam-2').some(e => e.source === 'signalr-subscribe' && e.ok === false
    && /quyền/.test(e.error)), 'the failed join must carry the server reason')
  // HTTP polling vẫn là kênh dự phòng: không có exception tràn ra khỏi hook.
})

test('camera_analysis events reach only the matching camera and are recorded', async () => {
  const f = fixture()
  const got = []
  f.context.subscribeCameraAnalysis('CAM-1', payload => got.push(payload))
  const other = []
  f.context.subscribeCameraAnalysis('CAM-2', payload => other.push(payload))
  const event = { cameraId: 'CAM-1', frameId: 'f1', rtpTimestamp: 90000 }
  f.push(event)
  assert.deepEqual(got, [event])
  assert.deepEqual(other, [], 'a pushed frame must never leak to another camera')
  assert.ok(f.diagnostics.get('cam-1')?.some(e => e.source === 'signalr-event' && e.frameId === 'f1'),
    'the received push must be diagnosable per camera')
  assert.deepEqual(f.diagnostics.get('cam-2'), undefined, 'an unrelated camera must stay clean')
})

test('reconnecting to the hub re-subscribes every active camera', async () => {
  const f = fixture()
  f.context.subscribeCameraAnalysis('CAM-1', () => {})
  f.context.subscribeCameraAnalysis('CAM-2', () => {})
  await f.flush()
  f.invokes.length = 0
  f.fire('disconnected')
  await f.flush()
  assert.equal(f.invokes.length, 0, 'a disconnected hub must not join groups')
  f.fire('connected')
  await f.flush()
  assert.deepEqual(f.invokes.map(i => i.name).sort(), ['SubscribeCameraAnalysis', 'SubscribeCameraAnalysis'])
  assert.deepEqual(new Set(f.invokes.map(i => i.args[0])), new Set(['cam-1', 'cam-2']))
  assert.ok(f.diagnostics.get('signalr')?.some(e => e.source === 'signalr-state' && e.state === 'connected'))
  assert.ok(f.diagnostics.get('signalr')?.some(e => e.source === 'signalr-state' && e.state === 'disconnected'))
})

test('unsubscribing the last viewer leaves the camera group', async () => {
  const f = fixture()
  const leave = f.context.subscribeCameraAnalysis('CAM-1', () => {})
  await f.flush()
  leave()
  await f.flush()
  assert.deepEqual(f.invokes.at(-1), { name: 'UnsubscribeCameraAnalysis', args: ['cam-1'] })
})
