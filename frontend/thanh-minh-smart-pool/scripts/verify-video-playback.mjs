import { createInterface } from 'node:readline'
import { pathToFileURL } from 'node:url'
import path from 'node:path'

const reader = createInterface({ input: process.stdin })
let settings
for await (const line of reader) { settings = JSON.parse(line); break }
process.env.VITE_API_BASE_URL = settings.apiBase
process.env.VITE_SIGNALR_HUB_URL = settings.apiBase
const frontend = path.join(settings.root, 'frontend/thanh-minh-smart-pool')
const { createServer } = await import(pathToFileURL(path.join(frontend, 'node_modules/vite/dist/node/index.js')).href)
const { chromium } = await import(pathToFileURL(process.env.SMARTPOOL_PLAYWRIGHT_MODULE).href)
const vite = await createServer({ root: frontend, server: { host: '127.0.0.1', port: 0, strictPort: false } })
await vite.listen()
let browser
try {
  browser = await chromium.launch({ channel: 'chrome', headless: true })
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } })
  await context.addInitScript(({ token, userId, jobId }) => {
    sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
      id: userId, username: 'playback-test', role: 'ADMIN', roles: ['ADMIN'], accessToken: token,
      expiresAtUtc: new Date(Date.now() + 240000).toISOString(),
    } }, version: 0 }))
    localStorage.setItem('safety-analysis-job', jobId)
  }, settings)
  const page = await context.newPage()
  const streamStatuses = []
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('response', response => {
    if (new URL(response.url()).pathname.endsWith(`/${settings.jobId}/stream`)) streamStatuses.push(response.status())
  })
  // Reproduce the failing bulk-download route. Playback must not depend on that XHR anymore.
  await page.route(`**/api/video-analyses/${settings.jobId}/video`, route => route.abort('failed'))
  await page.goto(new URL('/admin/safety/video-analysis', vite.resolvedUrls.local[0]).href)
  await page.locator('video').waitFor({ timeout: 20000 })
  await page.waitForFunction(() => document.querySelector('video')?.readyState >= 1, null, { timeout: 20000 })
  const dimensions = await page.locator('video').evaluate(video => ({ width: video.videoWidth, height: video.videoHeight, duration: video.duration }))
  if (!(dimensions.width > 0 && dimensions.height > 0 && dimensions.duration > 0)) throw new Error('Video metadata unavailable')
  await page.locator('video').evaluate(video => video.play())
  await page.waitForFunction(() => document.querySelector('video')?.currentTime > 0.1)
  await page.locator('video').evaluate(video => { video.currentTime = Math.max(0.3, video.duration / 2) })
  await page.waitForFunction(() => { const video = document.querySelector('video'); return video?.currentTime > video.duration / 2 + 0.1 })
  if (!streamStatuses.includes(206)) throw new Error('Browser did not receive HTTP Range video')
  const downloading = page.waitForEvent('download')
  await page.getByRole('link', { name: 'Tải video kết quả', exact: true }).click()
  const download = await downloading
  if (!download.suggestedFilename().endsWith('.mp4')) throw new Error('Download filename is invalid')
  if (errors.length) throw new Error(errors.join('\n'))
  await page.screenshot({ path: path.join(settings.root, 'backend/.test-runtime/video-playback-range.png'), fullPage: true })
  console.log('Chrome passed: HTTP 206 playback, advancing frames, seek and MP4 download; old bulk XHR blocked.')
} finally { await browser?.close(); await vite.close() }
