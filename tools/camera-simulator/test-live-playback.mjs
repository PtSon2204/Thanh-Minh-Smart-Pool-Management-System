import path from 'node:path'
import fs from 'node:fs/promises'
import { pathToFileURL } from 'node:url'
let input = ''
for await (const chunk of process.stdin) input += chunk
const { apiBase, token, userId, root } = JSON.parse(input)
const frontend = path.join(root, 'frontend/thanh-minh-smart-pool')
process.env.VITE_API_BASE_URL = apiBase
process.env.VITE_SIGNALR_HUB_URL = apiBase
const { createServer } = await import(pathToFileURL(path.join(frontend, 'node_modules/vite/dist/node/index.js')).href)
const { chromium } = await import(pathToFileURL(path.join(root, 'backend/.test-runtime/ui-test/node_modules/playwright-core/index.mjs')).href)
const vite = await createServer({ root: frontend, mode: 'camera-test', logLevel: 'silent', server: { host: '127.0.0.1', port: 0, strictPort: false } })
await vite.listen()
const browser = await chromium.launch({ channel: 'chrome', headless: true })
try {
  const context = await browser.newContext({ viewport: { width: 1600, height: 1600 } })
  if (process.env.SMARTPOOL_LIVE_PERF_RAW === '1') await context.route('**/camera/views', route => {
    if (route.request().method() !== 'POST') return route.continue()
    return route.continue({ postData: JSON.stringify({ ...route.request().postDataJSON(), analysis: false }) })
  })
  await context.addInitScript(({ token, userId }) => {
    sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: { id: userId, role: 'ADMIN', roles: ['ADMIN'], username: 'Camera performance test', accessToken: token, expiresAtUtc: new Date(Date.now() + 300000).toISOString() } }, version: 0 }))
    window.__peers = []
    window.__rtcTrace = []
    const NativePeer = window.RTCPeerConnection
    window.RTCPeerConnection = class extends NativePeer { constructor(...args) { super(...args); window.__peers.push(this) } }
    setInterval(async () => {
      for (const [index, peer] of window.__peers.entries()) {
        if (peer.connectionState === 'closed') continue
        const sample = { index, state: peer.connectionState, ice: peer.iceConnectionState, time: performance.now() }
        try { (await peer.getStats()).forEach(s => {
          if (s.type === 'inbound-rtp' && s.kind === 'video') Object.assign(sample, { packets: s.packetsReceived, received: s.framesReceived, decoded: s.framesDecoded, dropped: s.framesDropped })
          if (s.type === 'candidate-pair' && s.nominated) Object.assign(sample, { pair: s.state, bytes: s.bytesReceived })
        }) } catch {}
        window.__rtcTrace.push(sample)
      }
      if (window.__rtcTrace.length > 300) window.__rtcTrace.splice(0, 100)
    }, 1000)
    window.__longTasks = []
    const nativeFrameCallback = HTMLVideoElement.prototype.requestVideoFrameCallback
    HTMLVideoElement.prototype.requestVideoFrameCallback = function (callback) {
      return nativeFrameCallback.call(this, (time, metadata) => {
        const previous = window.__drawingRtp
        window.__drawingRtp = metadata.rtpTimestamp
        try { callback(time, metadata) } finally { window.__drawingRtp = previous }
      })
    }
    window.__analysisTrace = []
    const NativeSocket = window.WebSocket
    window.WebSocket = class extends NativeSocket {
      constructor(...args) {
        super(...args)
        this.addEventListener('message', event => {
          if (typeof event.data !== 'string') return
          for (const message of event.data.split('\x1e')) {
            try {
              const invocation = JSON.parse(message)
              if (invocation.target?.toLowerCase() !== 'camera_analysis') continue
              const data = invocation.arguments?.[0]
              window.__analysisTrace.push({ time: performance.now(), cameraId: data.cameraId,
                rtpTimestamp: data.rtpTimestamp, frameId: data.frameId, state: data.state, trackingSession: data.trackingSession,
                ageMs: Date.parse(data.serverTime) - Date.parse(data.observedAt), inferenceMs: data.inferenceMilliseconds,
                risks: data.risks, activeRiskTrackIds: data.activeRiskTrackIds, detections: data.detections })
              if (window.__analysisTrace.length > 6000) window.__analysisTrace.shift()
            } catch {}
          }
        })
      }
    }
    const nativeClear = CanvasRenderingContext2D.prototype.clearRect
    CanvasRenderingContext2D.prototype.clearRect = function (...args) {
      this.canvas.__boxes = []
      this.canvas.__boxRtpTimestamp = window.__drawingRtp
      return nativeClear.apply(this, args)
    }
    const nativeStroke = CanvasRenderingContext2D.prototype.strokeRect
    CanvasRenderingContext2D.prototype.strokeRect = function (...args) {
      this.canvas.dataset.boxDraws = String(Number(this.canvas.dataset.boxDraws || 0) + 1)
      ;(this.canvas.__boxes ||= []).push(args)
      return nativeStroke.apply(this, args)
    }
    new PerformanceObserver(list => window.__longTasks.push(...list.getEntries().map(e => e.duration))).observe({ type: 'longtask', buffered: false })
  }, { token, userId })
  const page = await context.newPage()
  const errors = []
  const responses = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('response', async response => {
    const endpoint = new URL(response.url()).pathname
    if (!endpoint.startsWith('/api/') || !(/analysis|views/.test(endpoint))) return
    try { const data = await response.json(); responses.push({ endpoint, status: response.status(), state: data.state, code: data.code, hasFrame: !!data.frameId }) } catch {}
  })
  await page.goto(new URL('/admin/safety/list-camera', vite.resolvedUrls.local[0]).href, { timeout: 60000 })
  await page.waitForFunction(() => [...document.querySelectorAll('video')].filter(v => v.readyState >= 2 && v.videoWidth > 0).length >= 3, null, { timeout: 30000 }).catch(async error => {
    await page.screenshot({ path: path.join(root, 'backend/.test-runtime/fluid-browser-failure.png') })
    const details = await page.evaluate(() => ({ body: document.body.innerText,
      videos: [...document.querySelectorAll('video')].map(v => ({ name: v.getAttribute('aria-label'), ready: v.readyState, width: v.videoWidth, quality: v.getVideoPlaybackQuality() })),
      peers: window.__peers.map(p => p.connectionState), trace: window.__rtcTrace }))
    await fs.writeFile(path.join(root, 'backend/.test-runtime/fluid-browser-failure.json'), JSON.stringify({ ...details, errors, responses }, null, 2))
    throw error
  })
  await page.evaluate(() => {
    window.__longTasks.length = 0
    window.__cadence = {}
    window.__rtpFrames = {}
    window.__overlayTrace = {}
    for (const video of document.querySelectorAll('video')) {
      const label = video.getAttribute('aria-label')
      const intervals = window.__cadence[label] = []
      const overlay = window.__overlayTrace[label] = []
      let previous
      const tick = (now, metadata) => {
        if (previous) intervals.push(now - previous)
        if (Number.isInteger(metadata.rtpTimestamp)) window.__rtpFrames[label] = (window.__rtpFrames[label] || 0) + 1
        const canvas = video.parentElement.querySelector('canvas')
        overlay.push({ time: now, rtpTimestamp: metadata.rtpTimestamp, boxRtpTimestamp: canvas?.__boxRtpTimestamp,
          canvasWidth: canvas?.clientWidth, canvasHeight: canvas?.clientHeight,
          videoWidth: video.videoWidth, videoHeight: video.videoHeight, boxes: canvas?.__boxes || [] })
        previous = now; video.requestVideoFrameCallback(tick)
      }
      video.requestVideoFrameCallback(tick)
    }
  })
  async function sample() {
    return page.evaluate(async () => {
      const result = []
      for (const video of document.querySelectorAll('video')) {
        const track = video.srcObject?.getVideoTracks()[0]
        const peer = window.__peers.find(p => p.getReceivers().some(r => r.track?.id === track?.id))
        let stats = {}
        if (peer) (await peer.getStats()).forEach(item => { if (item.type === 'inbound-rtp' && item.kind === 'video') stats = { decoded: item.framesDecoded, dropped: item.framesDropped, jitterBufferDelay: item.jitterBufferDelay, emitted: item.jitterBufferEmittedCount, packetsLost: item.packetsLost } })
        result.push({ name: video.getAttribute('aria-label'), ready: video.readyState, hidden: video.classList.contains('camera-video-hidden'), stats })
      }
      return { time: performance.now(), videos: result }
    })
  }
  const before = await sample()
  await page.waitForTimeout(Number(process.env.SMARTPOOL_LIVE_PERF_SECONDS || 15) * 1000)
  const after = await sample()
  await fs.writeFile(path.join(root, 'backend/.test-runtime/box-continuity-trace.json'), JSON.stringify(await page.evaluate(() => ({
    analysis: window.__analysisTrace, overlays: window.__overlayTrace,
  }))))
  const extra = await page.evaluate(() => ({ cadence: window.__cadence, rtpFrames: window.__rtpFrames,
    boxDraws: [...document.querySelectorAll('canvas')].map(c => Number(c.dataset.boxDraws || 0)), longTasks: window.__longTasks }))
  const elapsed = (after.time - before.time) / 1000
  const videos = after.videos.map((video, i) => {
    const old = before.videos.find(v => v.name === video.name)?.stats || {}
    const timings = [...(extra.cadence[video.name] || [])].sort((a, b) => a - b)
    return { ...video, fps: Number(((video.stats.decoded - old.decoded) / elapsed).toFixed(1)),
      presentedFps: Number((timings.length / elapsed).toFixed(1)), p95FrameGapMs: Number((timings[Math.floor(timings.length * .95)] || 0).toFixed(1)),
      maxFrameGapMs: Number((timings.at(-1) || 0).toFixed(1)), droppedDuringSample: video.stats.dropped - old.dropped,
      meanJitterBufferMs: Number(((video.stats.jitterBufferDelay - old.jitterBufferDelay) / (video.stats.emitted - old.emitted) * 1000).toFixed(1)) }
  })
  const first = page.locator('video').first()
  await first.dispatchEvent('waiting')
  await page.waitForTimeout(200)
  const shortStallPreservesFrame = await first.evaluate(video => !video.classList.contains('camera-video-hidden'))
  await first.dispatchEvent('playing')
  await first.dispatchEvent('waiting')
  await page.waitForTimeout(850)
  const longStallHidesStaleFrame = await first.evaluate(video => video.classList.contains('camera-video-hidden'))
  await first.dispatchEvent('playing')
  const report = { mode: process.env.SMARTPOOL_LIVE_PERF_RAW === '1' ? 'RAW' : 'SMOOTH_AI', elapsedSeconds: elapsed, videos,
    rtpFrames: extra.rtpFrames, boxDraws: extra.boxDraws, longTasksOver50ms: extra.longTasks.length, maxLongTaskMs: Math.max(0, ...extra.longTasks), shortStallPreservesFrame, longStallHidesStaleFrame, errors }
  await fs.writeFile(path.join(root, 'backend/.test-runtime/fluid-browser-results.json'), JSON.stringify(report, null, 2))
  await page.screenshot({ path: path.join(root, 'backend/.test-runtime/fluid-three-cameras.png') })
  if (errors.length || videos.length < 3 || videos.some(v => !Number.isFinite(v.fps) || v.fps < 20 || !extra.rtpFrames[v.name]) || !shortStallPreservesFrame || !longStallHidesStaleFrame) throw Error('Live playback regression; see fluid-browser-results.json')
  console.log(JSON.stringify(report))
  await context.close()
} finally { await browser.close(); await vite.close() }
