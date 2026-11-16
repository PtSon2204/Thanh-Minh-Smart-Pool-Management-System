import test from 'node:test'
import assert from 'node:assert/strict'
import { interpolateCameraBox } from './cameraBoxInterpolation.js'

const point = (timestamp, x, width = .1) => ({ timestamp, box: [x, .2, x + width, .4] })
const near = (a, b) => a.forEach((value, index) => assert.ok(Math.abs(value - b[index]) < 1e-9))

test('constant motion stays at the exact displayed time, including irregular AI sampling', () => {
  const previous = point(0, .1), before = point(9000, .2), after = point(27000, .4), following = point(36000, .5)
  for (let time = 9000; time <= 27000; time += 3600) {
    const box = interpolateCameraBox(before, after, (time - 9000) / 18000, previous, following)
    near(box, [.1 + time / 90000, .2, .2 + time / 90000, .4])
  }
})

test('measured endpoints are exact instead of slowly catching up over later frames', () => {
  const previous = point(0, .1), before = point(9000, .2), after = point(18000, .5)
  assert.deepEqual(interpolateCameraBox(before, after, 0, previous), before.box)
  assert.deepEqual(interpolateCameraBox(before, after, 1, previous), after.box)
  assert.deepEqual(interpolateCameraBox(before, after, 1.1, previous), after.box)
})

test('direction changes and noisy detections never overshoot the observed segment', () => {
  const previous = point(0, .8), before = point(9000, .1), after = point(18000, .2), following = point(27000, .1)
  for (let i = 0; i <= 100; i++) {
    const box = interpolateCameraBox(before, after, i / 100, previous, following)
    assert.ok(box[0] >= .1 - 1e-9 && box[0] <= .2 + 1e-9)
    assert.ok(box.every(value => value >= 0 && value <= 1))
  }
})

test('neighbouring segments meet with continuous velocity at an AI observation', () => {
  const points = [point(0, .1), point(9000, .15), point(18000, .3), point(27000, .4), point(36000, .45)]
  const epsilon = .00001
  const left = interpolateCameraBox(points[1], points[2], 1 - epsilon, points[0], points[3])[0]
  const right = interpolateCameraBox(points[2], points[3], epsilon, points[1], points[4])[0]
  const leftSpeed = (.3 - left) / epsilon, rightSpeed = (right - .3) / epsilon
  assert.ok(Math.abs(leftSpeed - rightSpeed) < .0001)
})

test('RTP wrap and size changes stay valid at the image boundary', () => {
  const previous = point(0xffffe000, .6), before = point(0xfffff000, .7, .2), after = point(0x1000, .95, .05)
  const box = interpolateCameraBox(before, after, .5, previous)
  assert.ok(box.every(value => value >= 0 && value <= 1))
  assert.ok(box[0] < box[2] && box[1] < box[3])
})
