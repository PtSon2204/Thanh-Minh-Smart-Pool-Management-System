// Regression specifications for the safety audit findings.
// No network, browser, credentials, or application data are used.
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState } from './alertState.js'
import { CameraRiskFrameBuffer } from './cameraRiskFrameBuffer.js'
import { ALERT_POPUP_LIFETIME_MS } from './popupLifetime.js'
import { resolveSignalRHubUrl } from '../../../services/signalrUrl.js'

test('AUDIT F01: a dismissed WARNING must not suppress its later CRITICAL escalation', () => {
  const now = Date.parse('2026-11-13T00:00:00Z')
  const critical = { id: 'same-warning-id', sourceType: 'LIVE', status: 'PENDING',
    severity: 'CRITICAL', createdAt: new Date(now - 45000).toISOString(), observedAt: new Date(now).toISOString() }
  const visible = reconcilePendingAlerts([], [critical], new Set([popupIdentity({ ...critical, severity: 'WARNING' })]), now)
  assert.equal(visible.length, 1, 'Escalation needs its own dismissal/expiry identity')
  assert.equal(reconcilePendingAlerts([], [critical], new Set([popupIdentity(critical)]), now).length, 0,
    'Polling must not resurrect the same dismissed critical revision')
})

test('AUDIT F03: an explicitly active risk with fresh measured geometry should remain visible through a confidence dip', () => {
  const origin = Date.parse('2026-11-13T00:00:00Z')
  const buffer = new CameraRiskFrameBuffer()
  const risk = { trackId: '7', episode: 'episode', severity: 'CRITICAL', box: [.1, .1, .3, .3] }
  for (let ms = 0; ms <= 500; ms += 100) {
    const observed = new Date(origin + ms).toISOString()
    buffer.add({ state: 'Connected', frameId: String(ms), trackingSession: 'same-session',
      rtpTimestamp: 100000 + ms * 90, observedAt: observed, serverTime: observed, processedAt: observed,
      risks: ms === 0 ? [risk] : [], activeRiskTrackIds: ['7'],
      detections: [{ trackId: '7', confidence: .4, box: [.1 + ms / 10000, .1, .3 + ms / 10000, .3] }],
    }, origin + ms)
  }
  assert.equal(buffer.sample(145000, origin + 500).length, 1,
    'The person is still measured and the engine still marks this risk active')
})

test('a second camera viewer seeds its own timeline from the shared query cache without another fetch or websocket frame', () => {
  const now = Date.now()
  const box = [.1, .1, .3, .5]
  const cached = { state: 'Connected', cameraId: 'camera', frameId: 'cached', trackingSession: 'session',
    receivedAt: now, processedAt: new Date(now).toISOString(), observedAt: new Date(now).toISOString(), serverTime: new Date(now).toISOString(),
    rtpTimestamp: 10000, risks: [], activeRiskTrackIds: ['person'],
    displayRisks: [{ trackId: 'person', episode: 'episode', severity: 'CRITICAL', box }],
    detections: [{ trackId: 'person', confidence: .4, box }],
  }
  const cleanups = []
  const context = { Date, CameraRiskFrameBuffer,
    useState: initial => [typeof initial === 'function' ? initial() : initial, () => {}],
    useMemo: fn => fn(), useCallback: fn => fn, useEffect: fn => { cleanups.push(fn()) },
    useQuery: () => ({ data: cached, isError: false }), useQueryClient: () => ({}),
    useAuthStore: selector => selector({ session: {} }), signalRIdentity: () => 'identity',
    subscribeCameraAnalysis: () => () => {}, monitoringService: {}, setInterval: () => 0, clearInterval() {},
    cameraAnalysisAge: () => 0, MAX_OVERLAY_AGE_MS: 1000, MONITORING_STALE_MS: 10000,
  }
  const source = readFileSync(new URL('../hooks/useCameraRisks.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replace('export function ', 'function ')
  vm.createContext(context)
  vm.runInContext(source, context, { filename: 'useCameraRisks.js' })
  try {
    const hook = context.useCameraRisks('camera', true)
    assert.equal(hook.monitored, true)
    assert.equal(hook.overlayFresh, true)
    assert.equal(hook.risks.length, 1, 'Camera status tags must use the same display state as its boxes')
    assert.deepEqual(hook.ai.data.risks, [], 'The shared metadata must preserve the original evidence separately')
    assert.equal(hook.sampleRisks(10000).length, 1)
    assert.equal(hook.sampleRisks(10000)[0].severity, 'CRITICAL')
  } finally { cleanups.forEach(cleanup => cleanup?.()) }
})

test('AUDIT F01: escalation gets a full popup lifetime and a dismissed critical revision stays dismissed', () => {
  let now = Date.parse('2026-11-13T00:00:00Z')
  let nextTimer = 0, snapshot, cleanups = []
  const timers = new Map(), handlers = new Map()
  class Clock extends Date { static now() { return now } }
  const context = {
    Date: Clock, AbortController, isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState,
    ALERT_POPUP_LIFETIME_MS,
    useCallback: fn => fn, useRef: value => ({ current: value }),
    useState: initial => [initial, value => { snapshot = value }],
    useEffect: fn => { cleanups.push(fn()) },
    useAuthStore: selector => selector({ session: { id: 'operator', accessToken: 'fixture' } }),
    signalRIdentity: session => session.id,
    getSignalRConnection: () => ({ on: (event, fn) => handlers.set(event, fn), off() {} }),
    startSignalRConnection: async () => true, onSignalRStateChange: () => () => {},
    monitoringService: { alerts: () => new Promise(() => {}) },
    subscribeCameraAnalysis: () => () => {},
    window: { addEventListener() {}, removeEventListener() {} },
    setTimeout: (fn, ms) => { const id = ++nextTimer; timers.set(id, { fn, due: now + ms }); return id },
    clearTimeout: id => timers.delete(id), setInterval: () => 0, clearInterval() {},
  }
  const source = readFileSync(new URL('../hooks/useGlobalSafetyAlerts.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replace('export function ', 'function ')
  vm.createContext(context)
  vm.runInContext(source, context, { filename: 'useGlobalSafetyAlerts.js' })
  const hook = context.useGlobalSafetyAlerts(true)
  const send = severity => handlers.get('alert_escalated')({ id: 'alert-A', sourceType: 'LIVE', status: 'PENDING', severity,
    createdAt: new Date(now).toISOString(), observedAt: new Date(now).toISOString() })
  const advance = ms => {
    now += ms
    for (const [id, timer] of [...timers]) if (timer.due <= now) { timers.delete(id); timer.fn() }
  }
  try {
    send('WARNING')
    advance(ALERT_POPUP_LIFETIME_MS - 1000)
    send('CRITICAL')
    advance(2000)
    assert.equal(snapshot.alerts.length, 1, 'The old WARNING timeout must not close the escalation')
    advance(ALERT_POPUP_LIFETIME_MS - 2000)
    assert.equal(snapshot.alerts.length, 0)
    send('CRITICAL')
    assert.equal(snapshot.alerts.length, 0, 'Same critical revision must not return after expiry')
    send('WARNING')
    hook.dismiss('alert-A')
    send('CRITICAL')
    assert.equal(snapshot.alerts.length, 0, 'A previously dismissed critical revision remains dismissed')
  } finally { cleanups.forEach(cleanup => cleanup?.()) }
})

test('LIVE popup keeps ten seconds through delayed warnings and incomplete HTTP resync, without extending duplicates', async () => {
  let now = Date.parse('2026-11-13T00:00:00Z')
  let snapshot, cleanups = [], poll, timerId = 0
  const timers = new Map(), handlers = new Map()
  class Clock extends Date { static now() { return now } }
  const context = {
    Date: Clock, AbortController, isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState,
    ALERT_POPUP_LIFETIME_MS,
    useCallback: fn => fn, useRef: value => ({ current: value }),
    useState: initial => [initial, value => { snapshot = value }], useEffect: fn => { cleanups.push(fn()) },
    useAuthStore: selector => selector({ session: { id: 'operator' } }), signalRIdentity: session => session.id,
    getSignalRConnection: () => ({ on: (event, fn) => handlers.set(event, fn), off() {} }),
    startSignalRConnection: async () => true, onSignalRStateChange: () => () => {},
    monitoringService: { alerts: async () => ({ items: [], totalCount: 0 }), alert: async () => { throw new Error('temporary failure') } },
    subscribeCameraAnalysis: () => () => {},
    window: { addEventListener() {}, removeEventListener() {} },
    setTimeout: (fn, ms) => { const id = ++timerId; timers.set(id, { fn, due: now + ms }); return id },
    clearTimeout: id => timers.delete(id), setInterval: fn => { poll = fn; return 1 }, clearInterval() {},
  }
  const source = readFileSync(new URL('../hooks/useGlobalSafetyAlerts.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replace('export function ', 'function ')
  vm.createContext(context)
  vm.runInContext(source, context)
  context.useGlobalSafetyAlerts(true)
  const settle = async () => { for (let i = 0; i < 12; i++) await Promise.resolve() }
  const advance = ms => {
    now += ms
    for (const [id, timer] of [...timers]) if (timer.due <= now) { timers.delete(id); timer.fn() }
  }
  const send = alert => handlers.get('alert_escalated')(alert)
  try {
    await settle()
    const warning = { id: 'same', sourceType: 'LIVE', status: 'PENDING', severity: 'WARNING',
      observedAt: new Date(now).toISOString(), createdAt: new Date(now).toISOString() }
    send(warning)
    advance(8000)
    const critical = { ...warning, severity: 'CRITICAL', observedAt: new Date(now).toISOString() }
    send(critical)
    advance(500)
    send(warning)
    assert.equal(snapshot.alerts[0].severity, 'CRITICAL', 'Delayed warning cannot remove escalation')
    poll()
    await settle()
    assert.equal(snapshot.alerts.length, 1, 'Missing row and failed detail lookup cannot close a visible popup')
    advance(2500)
    assert.equal(snapshot.alerts.length, 1, 'Original warning timer cannot close the critical popup')
    advance(6999)
    send(critical)
    assert.equal(snapshot.alerts.length, 1)
    advance(1)
    assert.equal(snapshot.alerts.length, 0, 'Exactly ten seconds after escalation; duplicates do not extend it')
    send(critical)
    assert.equal(snapshot.alerts.length, 0, 'Expired revision must not reopen')
    const fresh = { ...warning, id: 'another', observedAt: new Date(now).toISOString() }
    send(fresh)
    context.monitoringService.alert = async () => ({ ...fresh, status: 'ACKNOWLEDGED' })
    poll()
    await settle()
    assert.equal(snapshot.alerts.length, 0, 'A confirmed handled detail still closes the popup immediately')
  } finally { cleanups.forEach(cleanup => cleanup?.()) }
})

test('a LIVE popup follows the camera risk: refreshed while the track persists, closed when it clears', () => {
  let now = Date.parse('2026-11-13T00:00:00Z')
  let nextTimer = 0, snapshot, cleanups = []
  const timers = new Map(), handlers = new Map(), cameraSubscribers = new Map()
  class Clock extends Date { static now() { return now } }
  const context = {
    Date: Clock, AbortController, isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState,
    ALERT_POPUP_LIFETIME_MS,
    useCallback: fn => fn, useRef: value => ({ current: value }),
    useState: initial => [initial, value => { snapshot = value }],
    useEffect: fn => { cleanups.push(fn()) },
    useAuthStore: selector => selector({ session: { id: 'operator' } }),
    signalRIdentity: session => session.id,
    getSignalRConnection: () => ({ on: (event, fn) => handlers.set(event, fn), off() {} }),
    startSignalRConnection: async () => true, onSignalRStateChange: () => () => {},
    monitoringService: { alerts: async () => ({ items: [], totalCount: 0 }), alert: async () => { throw new Error('offline') } },
    subscribeCameraAnalysis: (cameraId, listener) => {
      cameraSubscribers.set(cameraId, listener)
      return () => cameraSubscribers.delete(cameraId)
    },
    window: { addEventListener() {}, removeEventListener() {} },
    setTimeout: (fn, ms) => { const id = ++nextTimer; timers.set(id, { fn, due: now + ms }); return id },
    clearTimeout: id => timers.delete(id), setInterval: () => 0, clearInterval() {},
  }
  const source = readFileSync(new URL('../hooks/useGlobalSafetyAlerts.js', import.meta.url), 'utf8')
    .replace(/^import[\s\S]*?from ['"][^'"]+['"]\r?\n/gm, '').replace('export function ', 'function ')
  vm.createContext(context)
  vm.runInContext(source, context, { filename: 'useGlobalSafetyAlerts.js' })
  context.useGlobalSafetyAlerts(true)
  const advance = ms => {
    now += ms
    for (const [id, timer] of [...timers]) if (timer.due <= now) { timers.delete(id); timer.fn() }
  }
  const frame = tracks => cameraSubscribers.get('cam-1')({
    cameraId: 'CAM-1', state: 'Connected', activeRiskTrackIds: tracks, displayRisks: [],
  })
  const send = (id, severity, trackId) => handlers.get('alert_created')({ id, sourceType: 'LIVE', status: 'PENDING', severity,
    cameraId: 'CAM-1', trackId, createdAt: new Date(now).toISOString(), observedAt: new Date(now).toISOString() })
  try {
    send('alert-A', 'WARNING', '7')
    assert.ok(cameraSubscribers.has('cam-1'), 'Popup must subscribe its camera stream while open')
    advance(9000)
    frame(['7'])
    advance(9000)
    assert.equal(snapshot.alerts.length, 1, 'Live risk on the camera refreshes the fallback timer and keeps the popup open')
    frame([])
    assert.equal(snapshot.alerts.length, 0, 'The camera clearing the risk must close the open popup immediately')
    send('alert-A', 'WARNING', '7')
    assert.equal(snapshot.alerts.length, 0, 'A closed alert revision must not reopen')
    send('alert-B', 'WARNING', '9')
    assert.ok(cameraSubscribers.has('cam-1'), 'A new alert re-subscribes the camera stream')
    // Model the live stream: a present frame every window keeps resetting the timer.
    frame(['9']); advance(9000)
    frame(['9']); advance(9000)
    assert.equal(snapshot.alerts.length, 1, 'A continuous camera stream keeps the popup open past the fallback timer')
    frame([])
    assert.equal(snapshot.alerts.length, 0, 'Clearing the risk on the camera closes the popup immediately')
  } finally { cleanups.forEach(cleanup => cleanup?.()); assert.ok(!cameraSubscribers.size, 'No camera subscription leaks past the effect') }
})

test('AUDIT F04: changing the login token must restart the existing SignalR connection', async () => {
  // Execute the real service with only transport/auth dependencies replaced.
  let token = 'token-A'
  let authChanged
  const authenticatedTokens = []
  const callbacks = new Map()
  class HubConnectionBuilder {
    withUrl(_url, options) { this.options = options; return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const options = this.options
      return { state: 'Disconnected', on(event, fn) { callbacks.set(event, fn) }, onreconnecting() {}, onreconnected() {}, onclose() {},
        async start() { authenticatedTokens.push(options.accessTokenFactory()); this.state = 'Connected' },
        async stop() { this.state = 'Disconnected' },
      }
    }
  }
  const signalR = { HubConnectionBuilder, HttpTransportType: { WebSockets: 1, LongPolling: 4 },
    HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' }, LogLevel: { Warning: 3 } }
  const url = new URL('../../../services/signalrClient.js', import.meta.url)
  const source = readFileSync(url, 'utf8').replace(/^import .*;\r?\n/gm, '')
    .replaceAll('import.meta.env.', 'environment.').replaceAll('export ', '')
  const context = { signalR, resolveSignalRHubUrl, setTimeout, clearTimeout, getAccessToken: () => token, useAuthStore: { subscribe(callback) { authChanged = callback } }, environment: {},
    window: { location: { origin: 'http://test.invalid' } }, console }
  vm.createContext(context)
  vm.runInContext(source, context, { filename: 'signalrClient.js' })
  await Promise.all([context.startSignalRConnection(), context.startSignalRConnection()])
  const received = []
  context.onReceiveNotification(message => received.push(message))
  token = 'token-B'
  callbacks.get('ReceiveNotification')('old-user-payload')
  assert.equal(received.length, 0, 'Do not deliver the old principal payload during identity transition')
  await context.startSignalRConnection()
  callbacks.get('ReceiveNotification')('current-user-payload')
  assert.deepEqual(received, ['current-user-payload'])
  assert.deepEqual(authenticatedTokens, ['token-A', 'token-B'],
    'accessTokenFactory only re-authenticates on a new transport connection')
  token = null
  authChanged({ session: null }, { session: { id: 'B', accessToken: 'token-B' } })
  assert.equal(await context.startSignalRConnection(), false)
  assert.equal(context.getSignalRConnection().state, 'Disconnected', 'Logout must close the authenticated transport')
})
