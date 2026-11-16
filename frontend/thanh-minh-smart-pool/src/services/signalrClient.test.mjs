import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { resolveSignalRHubUrl } from './signalrUrl.js'

test('SignalR URL accepts origin, hubs base and full endpoint without duplicating hubs', () => {
  const origin = 'http://localhost:5173'
  for (const base of ['https://localhost:7019', 'https://localhost:7019/',
    'https://localhost:7019/hubs', 'https://localhost:7019/hubs/',
    'https://localhost:7019/hubs/notifications', 'https://localhost:7019/hubs/notifications/',
    'https://localhost:7019/api/v1']) {
    assert.equal(resolveSignalRHubUrl(base, origin), 'https://localhost:7019/hubs/notifications')
  }
  assert.equal(resolveSignalRHubUrl('/hubs', origin), `${origin}/hubs/notifications`)
  assert.equal(resolveSignalRHubUrl(undefined, origin), `${origin}/hubs/notifications`)
  assert.equal(resolveSignalRHubUrl('https://host.test/pool/hubs', origin), 'https://host.test/pool/hubs/notifications')
})

function fixture() {
  let token = 'fixture-token', fail = true, starts = 0, authChanged, closed
  let timerId = 0, requestedUrl
  const timers = new Map()
  class HubConnectionBuilder {
    withUrl(url) { requestedUrl = url; return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Disconnected', on() {}, onreconnecting() {}, onreconnected() {}, onclose(fn) { closed = fn },
        async start() { starts++; if (fail) throw new Error('API temporarily unavailable'); this.state = 'Connected' },
        async stop() { this.state = 'Disconnected'; closed() },
      }
    }
  }
  const context = {
    signalR: { HubConnectionBuilder, HttpTransportType: { WebSockets: 1, LongPolling: 4 }, LogLevel: { Warning: 3 },
      HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' } },
    resolveSignalRHubUrl, getAccessToken: () => token,
    useAuthStore: { subscribe(fn) { authChanged = fn } },
    environment: { VITE_SIGNALR_HUB_URL: 'https://localhost:7019/hubs' },
    window: { location: { origin: 'http://localhost:5173' } }, console,
    setTimeout(fn, delay) { const id = ++timerId; timers.set(id, { fn, delay }); return id },
    clearTimeout(id) { timers.delete(id) },
  }
  const source = readFileSync(new URL('./signalrClient.js', import.meta.url), 'utf8')
    .replace(/^import[^\n]*\r?\n/gm, '').replaceAll('import.meta.env.', 'environment.').replaceAll('export ', '')
  vm.createContext(context)
  vm.runInContext(source, context)
  return { context, timers, get starts() { return starts }, get url() { return requestedUrl },
    recover() { fail = false },
    logout() { token = null; authChanged({ session: null }, { session: { id: 'operator', accessToken: 'fixture-token' } }) },
    disconnect() { context.getSignalRConnection().state = 'Disconnected'; closed() },
    async retry() {
      const [id, timer] = timers.entries().next().value
      timers.delete(id); timer.fn()
      for (let i = 0; i < 12; i++) await Promise.resolve()
    },
  }
}

test('failed initial SignalR handshake retries with backoff and recovers without page reload', async () => {
  const f = fixture()
  const states = []
  f.context.onSignalRStateChange(state => states.push(state))
  assert.equal(await f.context.startSignalRConnection(), false)
  assert.equal(f.url, 'https://localhost:7019/hubs/notifications')
  assert.equal([...f.timers.values()][0].delay, 2000)
  await f.retry()
  assert.equal(f.starts, 2)
  assert.equal([...f.timers.values()][0].delay, 5000)
  f.recover()
  await f.retry()
  assert.equal(f.context.getSignalRConnection().state, 'Connected')
  assert.equal(f.timers.size, 0)
  assert.deepEqual(states, ['error', 'error', 'connected'])
  f.disconnect()
  assert.equal([...f.timers.values()][0].delay, 2000, 'Backoff resets after recovery and retries after reconnect exhaustion')
})

test('logout cancels the pending initial connection retry', async () => {
  const f = fixture()
  await f.context.startSignalRConnection()
  assert.equal(f.timers.size, 1)
  f.logout()
  await f.context.startSignalRConnection()
  assert.equal(f.timers.size, 0)
  assert.equal(f.starts, 1)
})
