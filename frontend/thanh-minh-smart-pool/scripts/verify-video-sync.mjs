import { createInterface } from 'node:readline'
import { pathToFileURL } from 'node:url'
import path from 'node:path'
import assert from 'node:assert/strict'
import { writeFile } from 'node:fs/promises'

let settings
for await (const line of createInterface({ input: process.stdin })) { settings = JSON.parse(line.replace(/^\uFEFF/, '')); break }
process.env.VITE_API_BASE_URL = settings.apiBase
process.env.VITE_SIGNALR_HUB_URL = settings.apiBase
const frontend = path.join(settings.root, 'frontend/thanh-minh-smart-pool')
const { createServer } = await import(pathToFileURL(path.join(frontend, 'node_modules/vite/dist/node/index.js')).href)
const { chromium } = await import(pathToFileURL(process.env.SMARTPOOL_PLAYWRIGHT_MODULE).href)
const vite = await createServer({ root: frontend, server: { host: '127.0.0.1', port: 0,
  proxy: { '/api': { target: settings.apiBase, changeOrigin: true } } } })
await vite.listen()
let browser
try {
  browser = await chromium.launch({ channel: 'chrome', headless: true })
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } })
  await context.addInitScript(({ token, id }) => {
    sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
      id, username: 'video-sync-test', role: 'ADMIN', roles: ['ADMIN'], accessToken: token,
      expiresAtUtc: new Date(Date.now() + 300000).toISOString(),
    } }, version: 0 }))
    window.__draws = []
    window.__mediaEvents = []
    for (const name of ['loadstart', 'loadedmetadata', 'loadeddata', 'canplay', 'playing', 'pause', 'waiting']) {
      document.addEventListener(name, event => {
        if (event.target instanceof HTMLVideoElement && window.__startupClickAt)
          window.__mediaEvents.push({ name, at: performance.now() - window.__startupClickAt, time: event.target.currentTime })
      }, true)
    }
    document.addEventListener('playing', event => {
      const video = event.target
      if (video instanceof HTMLVideoElement && window.__startupClickAt && !window.__firstPresentedAt) {
        video.requestVideoFrameCallback(() => { window.__firstPresentedAt ??= performance.now() })
      }
    }, true)
    const original = CanvasRenderingContext2D.prototype.strokeRect
    CanvasRenderingContext2D.prototype.strokeRect = function (...args) {
      const video = document.querySelector('video[aria-label="Video phân tích đồng bộ cảnh báo"]')
      if (video) window.__draws.push({ time: video.currentTime, color: this.strokeStyle })
      return original.apply(this, args)
    }
  }, { token: settings.token, id: settings.userId })
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', failure => errors.push(failure.message))
  const startupNetwork = []
  let measuringStartup = true
  page.on('response', async response => {
    if (!measuringStartup || !response.url().includes('/api/video-analyses')) return
    try {
      const at = await page.evaluate(() => window.__startupClickAt ? performance.now() - window.__startupClickAt : null)
      if (at == null) return
      const data = response.url().includes('/window') ? await response.json() : null
      startupNetwork.push({ at, path: new URL(response.url()).pathname, status: response.status(), readyFrames: data?.readyFrames })
    } catch { /* Navigation can dispose a pending response. */ }
  })
  // First measure real inference-to-playback startup without intercepting metadata.
  await page.goto(new URL('/admin/safety/video-analysis', vite.resolvedUrls.local[0]).href)
  await page.locator('input[type=file]').setInputFiles(settings.input)
  const begin = page.getByRole('button', { name: 'Bắt đầu phân tích', exact: true })
  await begin.waitFor()
  await page.waitForFunction(() => ![...document.querySelectorAll('button')].find(b => b.textContent === 'Bắt đầu phân tích')?.disabled)
  await page.evaluate(() => { window.__startupClickAt = performance.now() })
  await begin.click()
  await page.waitForFunction(() => window.__firstPresentedAt != null)
  const realStartupMs = await page.evaluate(() => window.__firstPresentedAt - window.__startupClickAt)
  const realMediaEvents = await page.evaluate(() => window.__mediaEvents)
  measuringStartup = false
  const realJobId = await page.evaluate(() => localStorage.getItem('safety-analysis-job'))
  // Wait for the measured upload to finish before starting the timing fixture below.
  await page.getByText('Trạng thái: Đã hoàn tất', { exact: true }).waitFor({ timeout: 60000 })
  assert.ok(realStartupMs > 0)
  let allowAll = false
  // Deterministic WARNING/CRITICAL frames validate browser timing independently of
  // the ML classes found in the fixture; upload, playback, job and auth use the real API.
  await page.route('**/api/video-analyses/*/frame-analysis/window*', async route => {
    const url = new URL(route.request().url())
    const start = Math.floor(Number(url.searchParams.get('from') || 0) * 10)
    const ready = allowAll ? 22 : 4
    const frames = Array.from({ length: Math.max(0, ready - start) }, (_, offset) => {
      const index = start + offset
      return index >= 5 && index < 15 ? [{ trackId: 'test-person', episode: 'test-episode',
        severity: index >= 9 ? 'CRITICAL' : 'WARNING', timestamp: index / 10, ratio: .9, box: [.1, .2, .3, .4] }] : []
    })
    await route.fulfill({ json: { fps: 10, startFrame: start, readyFrames: ready, complete: allowAll, frames, legacy: false },
      headers: { 'Access-Control-Allow-Origin': '*' } })
  })
  await page.goto(new URL('/admin/safety/video-analysis', vite.resolvedUrls.local[0]).href)
  await page.getByRole('heading', { name: 'Phân tích Video', exact: true }).waitFor()
  await page.locator('input[type=file]').setInputFiles(settings.input)
  await page.getByRole('button', { name: 'Bắt đầu phân tích', exact: true }).click()
  await page.waitForFunction(() => {
    const video = document.querySelector('video[aria-label="Video phân tích đồng bộ cảnh báo"]')
    return video && video.currentTime > .1 && video.paused
  }, { timeout: 30000 })
  assert.ok(await page.locator('video').evaluate(video => video.currentTime < .5), 'Video must wait before unanalysed WARNING frames')
  assert.equal(await page.getByRole('alertdialog', { name: 'Cảnh báo trong video' }).count(), 0)
  allowAll = true
  await page.getByRole('alertdialog', { name: 'Cảnh báo trong video' }).waitFor()
  await page.waitForFunction(() => window.__draws.some(draw => draw.color.toLowerCase() === '#f97316'))
  await page.waitForFunction(() => window.__draws.some(draw => draw.color.toLowerCase() === '#ef4444'))
  const draws = await page.evaluate(() => window.__draws)
  const warning = draws.find(draw => draw.color.toLowerCase() === '#f97316')
  const critical = draws.find(draw => draw.color.toLowerCase() === '#ef4444')
  assert.ok(Math.abs(warning.time - .5) < .15, `Warning box out of sync: ${warning.time}`)
  assert.ok(Math.abs(critical.time - .9) < .15, `Critical box out of sync: ${critical.time}`)
  await page.screenshot({ path: path.join(settings.root, 'backend/.test-runtime/video-sync-popup.png'), fullPage: true })
  const originalSource = await page.locator('video').evaluate(video => video.currentSrc)
  assert.ok(originalSource.includes('original=true'))
  await page.locator('video').evaluate(video => { video.pause(); video.currentTime = .7 })
  await page.getByRole('alertdialog', { name: 'Cảnh báo trong video' }).getByText('Phát hiện dấu hiệu nguy cơ').waitFor()
  await page.locator('video').evaluate(video => { video.currentTime = 1.8 })
  await page.getByRole('alertdialog', { name: 'Cảnh báo trong video' }).waitFor({ state: 'detached' })
  await page.waitForFunction(() => {
    const canvas = document.querySelector('video[aria-label="Video phân tích đồng bộ cảnh báo"]')?.parentElement.querySelector('canvas')
    if (!canvas?.width) return false
    const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data
    for (let index = 3; index < pixels.length; index += 4) if (pixels[index]) return false
    return true
  })
  await page.getByText('Trạng thái: Đã hoàn tất', { exact: true }).waitFor({ timeout: 60000 })
  assert.equal(await page.locator('video').evaluate(video => video.currentSrc), originalSource)
  await page.screenshot({ path: path.join(settings.root, 'backend/.test-runtime/video-sync-ui.png'), fullPage: true })
  assert.deepEqual(errors, [])
  const report = { passed: true, realStartupMs, realJobId, startupNetwork, realMediaEvents,
    startupMeasurement: 'click to first presented video frame; real ONNX and HTTP, warm model; no metadata interception',
    warningFirstPaintSeconds: warning.time, criticalFirstPaintSeconds: critical.time,
    checks: ['auto-start', 'buffer-if-ai-behind', 'frame-aligned-box', 'popup-at-playhead', 'seek-clears-old-box', 'stable-source-on-completion'] }
  await writeFile(path.join(settings.root, 'backend/.test-runtime/video-sync-results.json'), JSON.stringify(report, null, 2))
  console.log(JSON.stringify(report))
} finally {
  await browser?.close()
  await vite.close()
}
