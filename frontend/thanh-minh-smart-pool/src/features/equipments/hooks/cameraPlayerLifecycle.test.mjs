import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import { CameraPlayoutBuffer } from '../../safety/utils/cameraPlayoutBuffer.js'

function loadHook(file, context) {
  vm.createContext(context)
  const source = readFileSync(new URL(file, import.meta.url), 'utf8')
    .replace(/^import[^\n]+\r?\n/gm, '').replace('export function ', 'function ')
  vm.runInContext(source, context)
}

function playoutFixture(buffered = false) {
  let cleanup, draw, present, health, time = 0
  const cancelledFrames = [], cancelledAnimations = [], clearedTimers = []
  const video = {
    requestVideoFrameCallback(callback) { draw = callback; return 11 },
    cancelVideoFrameCallback: id => cancelledFrames.push(id),
  }
  const risksRef = { current: [] }, playoutRef = { current: null }
  let fallback = false, paints = 0
  const context = {
    CameraPlayoutBuffer, performance: { now: () => time },
    document: { createElement: () => ({ getContext: () => ({}) }) },
    useEffect: effect => { cleanup = effect() },
    requestAnimationFrame: callback => { present = callback; return 22 },
    cancelAnimationFrame: id => cancelledAnimations.push(id),
    setInterval: callback => { health = callback; return 33 },
    clearInterval: id => clearedTimers.push(id),
  }
  loadHook('./useCameraPlayout.js', context)
  context.useCameraPlayout({ videoRef: { current: video }, playoutRef, risksRef,
    delayRef: { current: () => 300 }, samplerRef: { current: timestamp => [timestamp] },
    paintOverlay: () => { paints++ }, useSmooth: true, buffered, state: 'playing', cameraId: 'camera',
    setExactFallback: value => { fallback = value }, setBufferUnavailable: () => assert.fail('Unexpected buffer failure') })
  return { cleanup: () => cleanup(), draw: metadata => draw(0, metadata),
    present: now => present(now), stall: () => { time = 501; health() },
    risksRef, playoutRef, cancelledFrames, cancelledAnimations, clearedTimers,
    get fallback() { return fallback }, get paints() { return paints } }
}

test('unbuffered playback samples the displayed RTP, clears stalled boxes and cancels callbacks on cleanup', () => {
  const f = playoutFixture()
  f.draw({ rtpTimestamp: 90000 })
  assert.deepEqual(f.risksRef.current, [90000])
  f.stall()
  assert.equal(f.risksRef.current.length, 0)
  assert.equal(f.paints, 2)
  f.cleanup()
  assert.deepEqual(f.cancelledFrames, [11])
  assert.deepEqual(f.clearedTimers, [33])
  f.draw({ rtpTimestamp: 180000 })
  assert.equal(f.risksRef.current.length, 0, 'A late browser callback must not paint after disposal')
})

test('missing RTP selects paired playback and buffered cleanup releases retained VideoFrames', () => {
  const f = playoutFixture(true)
  let closed = 0
  f.playoutRef.current.add({ close: () => { closed++ } }, 90000, 0)
  f.draw({})
  assert.equal(f.fallback, true)
  f.cleanup()
  assert.equal(closed, 1)
  assert.equal(f.playoutRef.current, null)
  assert.deepEqual(f.cancelledAnimations, [22])
  f.present(1000)
  assert.equal(f.paints, 0)
})

test('stream cleanup aborts a pending connection, removes listeners and ignores a late configuration response', async () => {
  let cleanup, resolveConfiguration, signal, peers = 0
  const videoListeners = new Map(), windowListeners = new Map()
  const video = {
    addEventListener: (event, callback) => videoListeners.set(event, callback),
    removeEventListener: event => videoListeners.delete(event),
    pause() {}, removeAttribute() {}, load() {}, srcObject: null,
  }
  const context = {
    AbortController,
    useEffect: effect => { cleanup = effect() },
    window: {
      RTCPeerConnection: true,
      addEventListener: (event, callback) => windowListeners.set(event, callback),
      removeEventListener: event => windowListeners.delete(event),
    },
    RTCPeerConnection: class { constructor() { peers++ } },
    equipmentService: { getCameraViewConfiguration: (_id, abortSignal) => {
      signal = abortSignal
      return new Promise(resolve => { resolveConfiguration = resolve })
    } },
    clearInterval() {}, clearTimeout() {},
  }
  loadHook('./useCameraStream.js', context)
  const reconnectRef = { current: null }
  context.useCameraStream({ videoRef: { current: video }, reconnectRef, equipmentId: 'camera',
    attempt: 0, analysis: true, useSmooth: true, buffered: false, setError() {}, setState() {} })
  assert.equal(videoListeners.size, 4)
  assert.equal(windowListeners.size, 1)
  cleanup()
  assert.equal(signal.aborted, true)
  assert.equal(reconnectRef.current, null)
  assert.equal(videoListeners.size, 0)
  assert.equal(windowListeners.size, 0)
  resolveConfiguration({ iceServers: [] })
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(peers, 0, 'A response from an old viewer must not create a peer')
})
