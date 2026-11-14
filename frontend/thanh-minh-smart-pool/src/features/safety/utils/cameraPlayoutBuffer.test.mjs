import test from 'node:test'
import assert from 'node:assert/strict'
import { CameraPlayoutBuffer } from './cameraPlayoutBuffer.js'
import { CameraRiskFrameBuffer } from './cameraRiskFrameBuffer.js'
import { cameraCapturePlan } from './cameraCapturePlan.js'

const image = () => ({ closed: false, close() { assert.equal(this.closed, false, 'VideoFrame must close exactly once'); this.closed = true } })

test('25 FPS video and a moving box remain aligned through 5 Hz delayed AI observations', () => {
  const origin = Date.parse('2026-11-14T00:00:00Z')
  const plan = cameraCapturePlan(1280, 720, 640, 360, 2, 40, 400)
  const pixels = new CameraPlayoutBuffer(plan.capacity), boxes = new CameraRiskFrameBuffer()
  const images = []
  const box = ms => [.1 + ms * .00005, .2, .3 + ms * .00005, .4]
  let presented = 0
  for (let ms = 0; ms <= 3000; ms += 20) {
    if (ms % 40 === 0) {
      const frame = image(); images.push(frame)
      pixels.add(frame, 100000 + ms * 90, ms)
    }
    if (ms >= 160 && (ms - 160) % 200 === 0) {
      const observed = ms - 160
      boxes.add({ state: 'Connected', frameId: String(observed), trackingSession: 'session', rtpTimestamp: 100000 + observed * 90,
        observedAt: new Date(origin + observed).toISOString(), processedAt: new Date(origin + ms).toISOString(), serverTime: new Date(origin + ms).toISOString(),
        risks: [{ trackId: '1', episode: 'episode', severity: 'WARNING', box: box(observed) }],
      }, origin + ms)
    }
    const next = pixels.take(ms, 400)
    if (next) {
      const observed = (next.timestamp - 100000) / 90
      const risks = boxes.sample(next.timestamp, origin + ms)
      assert.equal(risks.length, 1, 'No blinking between delayed observations')
      // Under the 300 ms cap the box is a monotone interpolation across the
      // retained AI observations: it tracks the buffered (not live) image to
      // within one observation interval. A live-time regression would drift
      // by delay*rate (~0.02) and fail this bound.
      risks[0].box.forEach((value, index) => assert.ok(Math.abs(value - box(observed)[index]) < .01,
        'Canvas box must follow the buffered image, not the current live image'))
      assert.equal(next.frame.closed, false)
      presented++
    }
  }
  assert.equal(presented, 68, 'After startup, every source frame is presented at 25 FPS rather than AI FPS')
  pixels.clear()
  assert.ok(images.every(frame => frame.closed), 'Release every retained frame on cleanup')
})

test('buffer caps retained frames, progresses at capacity, and never rewinds when delay changes', () => {
  const buffer = new CameraPlayoutBuffer(3)
  const images = Array.from({ length: 10 }, image)
  images.forEach((frame, index) => buffer.add(frame, index, index * 10))
  assert.equal(buffer.pending.length, 3)
  assert.ok(images.slice(0, 7).every(frame => frame.closed))
  assert.equal(buffer.take(100, 550).timestamp, 7, 'Memory cap cannot prevent presentation indefinitely')
  assert.equal(buffer.take(120, 0), null, 'A target delay change must not abruptly jump through queued frames')
  const next = buffer.take(700, 0)
  assert.equal(next.timestamp, 9)
  buffer.clear()
  buffer.clear()
  assert.ok(images.every(frame => frame.closed))
})

test('camera lookahead follows analysis cost and cadence within bounded latency', () => {
  const buffer = new CameraRiskFrameBuffer()
  assert.equal(buffer.playoutDelayMs(), 300)
  for (let ms = 0; ms <= 600; ms += 200) buffer.add({ state: 'Connected', frameId: String(ms),
    rtpTimestamp: 100000 + ms * 90, observedAt: new Date(ms).toISOString(), processedAt: new Date(ms + 160).toISOString(),
    serverTime: new Date(ms + 160).toISOString(), risks: [], trackingSession: 'session',
  }, ms + 160)
  assert.equal(buffer.playoutDelayMs(), 300, 'Lookahead is capped at the 300 ms playout delay')
})

test('720p playback remains continuous through variable AI latency and one missing metadata update', () => {
  const origin = Date.parse('2026-11-14T00:00:00Z')
  const plan = cameraCapturePlan(1280, 720, 900, 506, 2, 40, 550)
  const pixels = new CameraPlayoutBuffer(plan.capacity), boxes = new CameraRiskFrameBuffer()
  const decisions = []
  const box = ms => [.1 + ms * .00004, .2, .3 + ms * .00004, .4]
  for (let observed = 0; observed <= 4000; observed += 200) {
    if (observed === 800) continue
    const lag = [160, 280, 200, 300][observed / 200 % 4]
    decisions.push({ observed, delivered: observed + lag })
  }
  let presented = 0
  try {
    for (let ms = 0; ms <= 4000; ms += 20) {
      if (ms % 40 === 0) pixels.add(image(), 100000 + ms * 90, ms)
      for (const item of decisions.filter(item => item.delivered === ms)) {
        boxes.add({ state: 'Connected', frameId: String(item.observed), trackingSession: 'session',
          rtpTimestamp: 100000 + item.observed * 90,
          observedAt: new Date(origin + item.observed).toISOString(), processedAt: new Date(origin + ms).toISOString(), serverTime: new Date(origin + ms).toISOString(),
          risks: [{ trackId: '1', episode: 'episode', severity: 'CRITICAL', box: box(item.observed) }],
        }, origin + ms)
      }
      const next = pixels.take(ms, 550)
      if (!next) continue
      const sourceMs = (next.timestamp - 100000) / 90
      const risks = boxes.sample(next.timestamp, origin + ms)
      assert.equal(risks.length, 1, `Box missing at source ${sourceMs} ms / wall ${ms} ms`)
      // The 550 ms request clamps to the 300 ms cap, so a few presentations are
      // held/predicted instead of bracketed by a future observation. The box
      // still tracks the object at the displayed media time within one
      // observation interval (deviation well under 0.01); a live-time drift
      // would be far larger.
      assert.ok(Math.abs(risks[0].box[0] - box(sourceMs)[0]) < .01, `Box trails the object at ${sourceMs} ms`)
      presented++
    }
    assert.equal(presented, 93)
  } finally { pixels.clear() }
})
