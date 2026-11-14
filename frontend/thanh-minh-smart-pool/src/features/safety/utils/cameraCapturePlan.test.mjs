import test from 'node:test'
import assert from 'node:assert/strict'
import { cameraCapturePlan } from './cameraCapturePlan.js'

test('720p/1080p sources retain the full lookahead at 25 and 60 FPS instead of being forced to six frames', () => {
  for (const [videoWidth, videoHeight] of [[1280, 720], [1920, 1080], [3840, 2160]]) {
    for (const fps of [25, 30, 60]) {
      for (const delay of [160, 300]) {
        const plan = cameraCapturePlan(videoWidth, videoHeight, 900, 506, 2, 1000 / fps, delay)
        assert.ok((plan.capacity - 2) * 1000 / fps >= delay + 80)
        assert.ok(plan.width * plan.height * 4 * (plan.capacity + 1) <= 48 * 1024 * 1024)
        assert.ok(plan.width <= videoWidth && plan.height <= videoHeight)
        assert.ok(Math.abs(plan.width / plan.height - videoWidth / videoHeight) < .01)
      }
    }
  }
})

test('retained frames fit the visible image without enlarging a small source', () => {
  assert.deepEqual(cameraCapturePlan(1280, 720, 640, 360, 1, 40, 400), { width: 640, height: 360, capacity: 12 })
  const small = cameraCapturePlan(320, 240, 1000, 600, 2, 40, 300)
  assert.equal(small.width, 320)
  assert.equal(small.height, 240)
  assert.equal(cameraCapturePlan(1280, 720, 0, 0, 1, 40, 400), null)
})
