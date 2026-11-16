import test from 'node:test'
import assert from 'node:assert/strict'
import { drawVideoRisks } from './drawVideoRisks.js'

const risk = severity => ({ trackId: '1', episode: 'one', severity, box: [.1, .2, .3, .4] })

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

test('buffered camera image and its box use the same dimensions and letterbox', () => {
  const previousWindow = globalThis.window
  globalThis.window = { devicePixelRatio: 1 }
  const images = [], boxes = []
  const context = { setTransform() {}, clearRect() {}, fillRect() {}, fillText() {}, measureText() { return { width: 40 } },
    drawImage(...args) { images.push(args) }, strokeRect(...args) { boxes.push(args) } }
  const canvas = { width: 0, height: 0, getContext: () => context }
  const frame = { displayWidth: 1600, displayHeight: 900 }
  try {
    // The live source can resize while an older decoded frame is still queued.
    drawVideoRisks(canvas, { clientWidth: 800, clientHeight: 600, videoWidth: 640, videoHeight: 480 }, [risk('WARNING')], frame)
    assert.deepEqual(images[0], [frame, 0, 75, 800, 450])
    assert.deepEqual(boxes[0], [80, 165, 160, 90])
  } finally { globalThis.window = previousWindow }
})
