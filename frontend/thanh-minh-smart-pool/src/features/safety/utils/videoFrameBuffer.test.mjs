import test from 'node:test'
import assert from 'node:assert/strict'
import { VideoFrameBuffer, videoWarningKey } from './videoFrameBuffer.js'
import { drawVideoRisks } from './drawVideoRisks.js'

const risk = severity => ({ trackId: '1', episode: 'one', severity, box: [.1, .2, .3, .4] })

test('frame selection follows presented mediaTime exactly, including empty frames', () => {
  const buffer = new VideoFrameBuffer()
  buffer.merge({ fps: 25, startFrame: 0, readyFrames: 3, complete: false, frames: [[], [risk('WARNING')], []] }, 0)
  assert.deepEqual(buffer.sample(.039).risks, [])
  assert.equal(buffer.sample(.04).risks[0].severity, 'WARNING')
  assert.deepEqual(buffer.sample(.08).risks, [])
})

test('playback ahead of AI or seeking outside the cache must wait rather than reuse an old box', () => {
  const buffer = new VideoFrameBuffer()
  buffer.merge({ fps: 10, startFrame: 0, readyFrames: 1, complete: false, frames: [[risk('WARNING')]] }, 0)
  assert.equal(buffer.sample(.5), null)
  assert.equal(buffer.ahead(.5), 0)
  assert.equal(buffer.sample(20), null)
})

test('prefetched metadata gives a smooth playback buffer without a per-frame network request', () => {
  const buffer = new VideoFrameBuffer()
  buffer.merge({ fps: 25, startFrame: 0, readyFrames: 100, complete: false, frames: Array.from({ length: 100 }, () => []) }, 0)
  assert.equal(buffer.ahead(0), 4)
  for (let frame = 0; frame < 100; frame++) assert.ok(buffer.sample(frame / 25))
})

test('completed metadata is readable through the final frame and EOF does not trigger buffering', () => {
  const buffer = new VideoFrameBuffer()
  buffer.merge({ fps: 10, startFrame: 0, readyFrames: 2, complete: true, frames: [[], [risk('CRITICAL')]] }, 0)
  assert.equal(buffer.sample(.1).risks[0].severity, 'CRITICAL')
  assert.equal(buffer.ahead(.1), Infinity)
  assert.deepEqual(buffer.sample(.2).risks, [])
})

test('seeking far ahead prunes old metadata; seeking back requires its own data window', () => {
  const buffer = new VideoFrameBuffer()
  buffer.merge({ fps: 10, startFrame: 0, readyFrames: 2, complete: false, frames: [[], []] }, 0)
  buffer.merge({ fps: 10, startFrame: 1000, readyFrames: 1002, complete: false, frames: [[], []] }, 100)
  assert.equal(buffer.sample(0), null)
  assert.ok(buffer.sample(100))
})

test('popup identity deduplicates a risk episode and permits a critical escalation', () => {
  assert.equal(videoWarningKey(risk('WARNING')), videoWarningKey(risk('WARNING')))
  assert.notEqual(videoWarningKey(risk('WARNING')), videoWarningKey(risk('CRITICAL')))
})

test('canvas boxes respect the video letterbox and display size', () => {
  const previousWindow = globalThis.window
  globalThis.window = { devicePixelRatio: 2 }
  const rectangles = []
  const context = { setTransform() {}, clearRect() {}, strokeRect(...args) { rectangles.push(args) }, fillRect() {}, fillText() {}, measureText() { return { width: 40 } } }
  const canvas = { width: 0, height: 0, getContext: () => context }
  drawVideoRisks(canvas, { clientWidth: 800, clientHeight: 600, videoWidth: 1600, videoHeight: 900 }, [risk('WARNING')])
  assert.deepEqual(rectangles[0], [80, 165, 160, 90])
  assert.equal(canvas.width, 1600)
  assert.equal(canvas.height, 1200)
  globalThis.window = previousWindow
})
