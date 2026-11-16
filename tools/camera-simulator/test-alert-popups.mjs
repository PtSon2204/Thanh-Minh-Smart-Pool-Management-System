// Browser regression test; API and SignalR are isolated fixtures, never production alerts.
// node tools/camera-simulator/test-alert-popups.mjs
// Optional: SMARTPOOL_PLAYWRIGHT_MODULE=/absolute/path/to/playwright-core/index.mjs
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')
const frontend = path.join(root, 'frontend/thanh-minh-smart-pool')
const output = path.join(root, 'backend/.test-runtime/popup-burst')
await fs.mkdir(output, { recursive: true })
process.env.VITE_API_BASE_URL = 'http://127.0.0.1:59999'
process.env.VITE_SIGNALR_HUB_URL = 'http://127.0.0.1:59999'
const { createServer } = await import(pathToFileURL(path.join(frontend, 'node_modules/vite/dist/node/index.js')).href)
const { chromium } = await import(pathToFileURL(process.env.SMARTPOOL_PLAYWRIGHT_MODULE || path.join(root, 'backend/.test-runtime/ui-test/node_modules/playwright-core/index.mjs')).href)
const vite = await createServer({ root: frontend, mode: 'camera-test', logLevel: 'silent', server: { host: '127.0.0.1', port: 0, strictPort: false } })
await vite.listen()
const browser = await chromium.launch({ channel: 'chrome', headless: true })
const report = { passed: false, fixtureOnly: true, checks: [], screenshots: [] }
const imageUrl = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="640" height="360"><rect width="640" height="360" fill="#0077b6"/><text x="20" y="60" fill="white" font-size="24">TEST SNAPSHOT</text></svg>')
try {
  for (const viewport of [{ width: 1440, height: 900 }, { width: 320, height: 740 }, { width: 844, height: 390 }]) {
    const context = await browser.newContext({ viewport })
    const page = await context.newPage()
    page.setDefaultTimeout(8000)
    page.setDefaultNavigationTimeout(60000)
    const errors = []
    page.on('pageerror', error => errors.push(error.message))
    const rows = new Map()
    const requests = []
    const closedViews = []
    const failOnce = new Set(['burst-6'])
    let releaseWarning
    const warningResponse = new Promise(resolve => { releaseWarning = resolve })
    let wire
    let ready = false
    await context.addInitScript(() => {
      sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
      id: 'popup-test', username: 'popup-test', role: 'ADMIN', roles: ['ADMIN'], accessToken: 'fixture',
      expiresAtUtc: new Date(Date.now() + 3600000).toISOString(),
      } }, version: 0 }))
      const callback = HTMLVideoElement.prototype.requestVideoFrameCallback
      if (callback) HTMLVideoElement.prototype.requestVideoFrameCallback = function (listener) {
        return callback.call(this, (now, metadata) => {
          if (this.getAttribute('aria-label') === 'Video camera Camera test 04') window.__popupPresentedRtp = metadata.rtpTimestamp
          listener(now, metadata)
        })
      }
    })
    await context.routeWebSocket('**/hubs/notifications**', socket => {
      wire = socket
      socket.onMessage(message => {
        if (String(message).includes('"protocol"')) { socket.send('{}\x1e'); ready = true }
      })
    })
    await context.route('**/hubs/**', route => route.fulfill({ json: { negotiateVersion: 1, connectionId: 'popup-test', connectionToken: 'popup-test',
      availableTransports: [{ transport: 'WebSockets', transferFormats: ['Text'] }] } }))
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url())
      const endpoint = url.pathname
      const cameraView = endpoint.match(/^\/api\/equipments\/camera-4\/camera\/views(?:\/([^/]+))?$/)
      if (cameraView) {
        const verb = route.request().method()
        if (verb === 'DELETE') {
          closedViews.push(cameraView[1])
          await page.evaluate(id => {
            const item = window.__popupCameraPeers?.get(id)
            if (item) { clearInterval(item.timer); item.stream.getTracks().forEach(track => track.stop()); item.peer.close(); window.__popupCameraPeers.delete(id) }
          }, cameraView[1])
          return route.fulfill({ status: 204 })
        }
        if (verb === 'POST') {
          const offer = route.request().postDataJSON()
          assert.equal(offer.analysis, true, 'LIVE details must request analyzed frames')
          assert.equal(offer.smooth, true, 'LIVE details must keep source FPS and sample the AI timeline')
          const answer = await page.evaluate(async offer => {
            const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 360
            const ctx = canvas.getContext('2d')
            const stream = canvas.captureStream(0)
            // Plain source pixels: the application must draw its own AI overlay.
            const paint = () => { ctx.fillStyle = '#0077b6'; ctx.fillRect(0, 0, 640, 360); stream.getVideoTracks()[0].requestFrame() }
            const peer = new RTCPeerConnection({ iceServers: [] })
            peer.addTrack(stream.getVideoTracks()[0], stream)
            await peer.setRemoteDescription({ type: offer.type, sdp: offer.sdp })
            await peer.setLocalDescription(await peer.createAnswer())
            if (peer.iceGatheringState !== 'complete') await new Promise(resolve => peer.addEventListener('icegatheringstatechange', () => { if (peer.iceGatheringState === 'complete') resolve() }))
            const id = crypto.randomUUID()
            window.__popupCameraPeers ||= new Map()
            window.__popupCameraPeers.set(id, { peer, stream, timer: setInterval(paint, 40) })
            paint()
            return { viewId: id, type: 'answer', sdp: peer.localDescription.sdp }
          }, offer)
          return route.fulfill({ json: answer })
        }
        return route.fulfill({ json: cameraView[1] ? { state: 'Connected' } : { iceServers: [] } })
      }
      if (endpoint === '/api/cameras/camera-4/analysis') {
        const stamp = await page.evaluate(() => ({ rtp: window.__popupPresentedRtp, now: Date.now() }))
        if (!Number.isInteger(stamp.rtp)) return route.fulfill({ json: { state: 'Offline', risks: [] } })
        const box = [220 / 640, 90 / 360, 320 / 640, 250 / 360]
        const time = new Date(stamp.now).toISOString()
        return route.fulfill({ json: { cameraId: 'camera-4', state: 'Connected', frameId: String(stamp.rtp), trackingSession: 'popup-camera-session',
          rtpTimestamp: stamp.rtp, processedAt: time, observedAt: time, serverTime: time,
          risks: [], activeRiskTrackIds: ['4'], detections: [{ trackId: '4', confidence: .4, class: 'drowning', box }],
          displayRisks: [{ trackId: '4', episode: 'popup-episode', severity: 'CRITICAL', box, timestamp: stamp.now / 1000 }],
        } })
      }
      const action = endpoint.match(/^\/api\/alerts\/(burst-\d+)\/(acknowledge|cancel)$/)
      if (action) {
        const [, id, verb] = action
        requests.push({ id, verb })
        if (id === 'burst-1') await warningResponse
        if (failOnce.delete(id)) return route.fulfill({ status: 503, json: { message: 'Lỗi tạm thời trong test' } })
        const row = rows.get(id)
        if (verb === 'acknowledge' && route.request().postDataJSON()?.expectedSeverity !== row.severity)
          return route.fulfill({ status: 409, json: { message: 'Mức cảnh báo đã thay đổi. Vui lòng kiểm tra mức mới rồi xác nhận lại.', alert: row } })
        const handled = { ...row, status: verb === 'acknowledge' ? 'ACKNOWLEDGED' : 'CANCELLED',
          incidentId: verb === 'acknowledge' && row.severity === 'CRITICAL' ? `fixture-incident-${id}` : null }
        rows.set(id, handled)
        return route.fulfill({ json: handled })
      }
      let body = { items: [], totalCount: 0 }
      const alertDetail = endpoint.match(/^\/api\/alerts\/(burst-\d+)$/)
      if (alertDetail) body = rows.get(alertDetail[1])
      else if (endpoint === '/api/alerts') {
        const all = [...rows.values()].filter(row => !url.searchParams.has('status') || row.status === 'PENDING')
        body = { items: all, totalCount: all.length }
      } else if (endpoint === '/api/cameras') {
        body = { items: [{ id: 'camera-4', name: 'Camera test 04', location: 'Bể test 4', status: 'Active', source: 'RTSP', connectionStatus: 'Connected', smoothPlayback: true }], totalCount: 1 }
      } else if (endpoint === '/api/cameras/areas') body = []
      return route.fulfill({ json: body })
    })
    try {
      await page.goto(new URL('/admin/safety/alerts', vite.resolvedUrls.local[0]).href)
      await page.locator('.safety-alert-table').waitFor()
      for (let attempt = 0; attempt < 100 && !ready; attempt++) await new Promise(resolve => setTimeout(resolve, 50))
      assert.ok(ready, 'Mock SignalR handshake must complete')
      await page.clock.install()
      await page.clock.pauseAt(new Date(Date.now() + 100))
      await page.addStyleTag({ content: '.ant-message,.ant-message *{pointer-events:none!important}' })
      const now = await page.evaluate(() => Date.now())
      const row = (number, createdAt = now) => ({ id: `burst-${number}`, sourceType: 'LIVE', status: 'PENDING', cameraId: `camera-${number}`,
        cameraName: `Camera test ${String(number).padStart(2, '0')}`, location: `Bể test ${number}`, severity: number % 2 ? 'WARNING' : 'CRITICAL',
        trackId: String(number), imageUrl, createdAt: new Date(createdAt).toISOString() })
      const emit = (name, alert) => { rows.set(alert.id, alert); wire.send(JSON.stringify({ type: 1, target: name, arguments: [alert] }) + '\x1e') }
      const cards = page.locator('.safety-popup-stack > .ant-alert')
      const card = number => cards.filter({ has: page.getByText(`Camera test ${String(number).padStart(2, '0')}`, { exact: true }) })
      async function flush() {
        await new Promise(resolve => setTimeout(resolve, 100))
        await page.clock.runFor(100)
      }
      async function advance(milliseconds) {
        // A real SignalR server sends heartbeats. Keep the mocked connection alive
        // while fast-forwarding beyond HubConnection's 30-second server timeout.
        while (milliseconds > 0) {
          wire.send('{"type":6}\x1e')
          await new Promise(resolve => setTimeout(resolve, 30))
          const step = Math.min(5000, milliseconds)
          await page.clock.fastForward(step)
          await new Promise(resolve => setTimeout(resolve, 30))
          milliseconds -= step
        }
      }
      async function count(expected) {
        // Poll from Node: page RAF/timers are deliberately frozen for lifetime assertions.
        for (let i = 0; i < 100 && await cards.count() !== expected; i++) await new Promise(resolve => setTimeout(resolve, 50))
        assert.equal(await cards.count(), expected)
      }
      async function click(button) {
        await button.evaluate(element => element.scrollIntoView({ block: 'center', behavior: 'instant' }))
        // A frozen success/error toast can cover a scrolled button, especially in landscape.
        const reachable = await button.evaluate(element => {
          const r = element.getBoundingClientRect()
          return element.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2))
        })
        if (!reachable) await page.clock.runFor(3500)
        await button.click({ force: true })
      }
      const check = name => { report.checks.push(`${viewport.width}x${viewport.height}: ${name}`); console.log(`PASS ${viewport.width}: ${name}`) }
      for (let number = 1; number <= 10; number++) emit('alert_created', row(number, now - (10 - number)))
      await count(10)
      assert.deepEqual(await cards.locator('strong').allTextContents(), Array.from({ length: 10 }, (_, i) => `Camera test ${String(10 - i).padStart(2, '0')}`))
      assert.equal(await cards.locator('img').count(), 10)
      check('10 simultaneous LIVE alerts, newest first, each with its own snapshot')
      for (let i = 0; i < 3; i++) emit('alert_created', rows.get('burst-3'))
      emit('alert_escalated', { ...rows.get('burst-3'), severity: 'CRITICAL' })
      await card(3).getByText('Khẩn cấp', { exact: true }).waitFor()
      assert.equal(await cards.count(), 10)
      check('duplicate events and escalation update one popup without duplicating it')
      const geometry = await page.locator('.safety-popup-stack').evaluate(element => {
        const rect = element.getBoundingClientRect()
        return { fits: rect.left >= 0 && rect.right <= innerWidth && rect.bottom <= innerHeight,
          scrolls: element.scrollHeight > element.clientHeight, pageFits: document.documentElement.scrollWidth <= innerWidth }
      })
      assert.deepEqual(geometry, { fits: true, scrolls: true, pageFits: true })
      const screenshot = path.join(output, `ten-live-${viewport.width}.png`)
      await page.screenshot({ path: screenshot, animations: 'disabled' })
      report.screenshots.push(path.relative(root, screenshot))
      check('stack scrolls inside viewport without horizontal overflow')
      await click(card(5).getByRole('button', { name: 'Đóng', exact: true }))
      await count(9)
      assert.equal(requests.length, 0)
      await click(card(1).getByRole('button', { name: 'Xác nhận', exact: true }))
      assert.equal(await card(1).getByRole('button', { name: /Xác nhận/ }).isDisabled(), true)
      assert.equal(await card(2).getByRole('button', { name: 'Xác nhận', exact: true }).isEnabled(), true)
      await click(card(2).getByRole('button', { name: 'Xác nhận', exact: true }))
      await count(8)
      releaseWarning()
      await count(7)
      assert.equal(rows.get('burst-1').status, 'ACKNOWLEDGED')
      assert.equal(rows.get('burst-1').incidentId, null)
      assert.equal(rows.get('burst-2').incidentId, 'fixture-incident-burst-2')
      assert.deepEqual(requests, [{ id: 'burst-1', verb: 'acknowledge' }, { id: 'burst-2', verb: 'acknowledge' }])
      check('simultaneous acknowledgements and close target only selected popups; no shared loading lock')
      await click(card(6).getByRole('button', { name: 'Xác nhận', exact: true }))
      await page.getByText('Lỗi tạm thời trong test', { exact: true }).waitFor()
      await page.clock.runFor(400)
      assert.equal(await cards.count(), 7)
      await click(card(6).getByRole('button', { name: /Xác nhận/ }))
      await count(6)
      check('failed acknowledgement keeps popup visible and permits retry')
      await page.addStyleTag({ content: '.ant-message{display:none!important}' })
      await click(card(7).getByRole('button', { name: 'Hủy', exact: true }))
      await count(5)
      assert.equal(rows.get('burst-7').status, 'CANCELLED')
      await click(card(4).getByRole('button', { name: 'Xem chi tiết', exact: true }))
      await count(4)
      assert.equal(new URL(page.url()).pathname, '/admin/safety/alerts')
      const liveDialog = page.getByRole('dialog', { name: 'Camera LIVE · Camera test 04' })
      await liveDialog.waitFor()
      await liveDialog.getByText('Bể test 4', { exact: true }).waitFor()
      const liveVideo = liveDialog.getByLabel('Video camera Camera test 04')
      for (let i = 0; i < 80; i++) {
        await flush()
        if (await liveVideo.count() && await liveVideo.evaluate(video => video.videoWidth > 0 && video.readyState >= 2)) break
      }
      assert.equal(await liveVideo.evaluate(video => video.videoWidth > 0 && video.readyState >= 2), true)
      const overlay = liveDialog.locator('canvas.camera-analysis-overlay')
      let painted = false
      for (let i = 0; i < 60 && !painted; i++) {
        await flush()
        if (await overlay.count()) painted = await overlay.evaluate(canvas => {
          if (!canvas.width || !canvas.height) return false
          const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data
          // Buffered video is opaque too: require a red box/label, not just alpha.
          return pixels.some((value, index) => index % 4 === 0 && value > 180 && pixels[index + 1] < 120 && pixels[index + 2] < 120)
        })
      }
      assert.equal(painted, true, 'Opening a new LIVE dialog must draw a box from displayRisks on plain video, even with risks=[]')
      check('new LIVE dialog draws real canvas boxes from current display metadata during a confidence dip')
      await page.clock.runFor(500)
      assert.equal(await liveDialog.evaluate(element => {
        for (let current = element; current; current = current.parentElement) if (Number(getComputedStyle(current).opacity) === 0) return false
        return true
      }), true, 'The live dialog must be visually opaque, not just mounted behind an entry transition')
      await page.screenshot({ path: path.join(output, `live-details-${viewport.width}.png`), animations: 'disabled' })
      report.screenshots.push(path.relative(root, path.join(output, `live-details-${viewport.width}.png`)))
      const dialogGeometry = await liveDialog.evaluate(element => { const r = element.getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth && r.bottom <= innerHeight })
      assert.equal(dialogGeometry, true)
      check('details open the matching LIVE video and alert information in a responsive popup without navigation')
      const elapsed = await page.evaluate(start => Date.now() - start, now)
      assert.ok(elapsed < 9000, 'Actions must complete before the ten-second popup expires')
      await advance(9000 - elapsed)
      await count(4)
      await advance(1500)
      await count(0)
      assert.equal(await liveDialog.isVisible(), true, 'Viewing camera details must outlive the notification expiry')
      // Open dialog: ACK/CANCEL stay available for the whole time it is on screen
      // (the alert is still PENDING), so they do not disappear at the ten-second mark.
      assert.equal(await liveDialog.getByRole('button', { name: 'Xác nhận', exact: true }).count(), 1,
        'An open live detail keeps its action buttons while the alert is pending')
      assert.equal(await liveDialog.getByRole('button', { name: 'Hủy', exact: true }).count(), 1,
        'An open live detail keeps its action buttons while the alert is pending')
      const oldActions = page.locator('.safety-alert-table').getByRole('button', { name: /^(Xác nhận|Hủy)$/ })
      for (let i = 0; i < 30 && await oldActions.count(); i++) await flush()
      assert.equal(await oldActions.count(), 0, 'Old camera rows must retain only detail actions')
      await click(liveDialog.getByRole('button', { name: 'Đóng', exact: true }))
      await liveDialog.waitFor({ state: 'hidden' })
      assert.equal(rows.get('burst-4').status, 'PENDING', 'Expiry must not acknowledge or cancel the database alert')
      assert.equal(rows.get('burst-4').incidentId, undefined)
      for (let i = 0; i < 40 && !closedViews.length; i++) await new Promise(resolve => setTimeout(resolve, 50))
      assert.ok(closedViews.length, 'Closing/handling details must release the WebRTC view')
      check('open details keep actions while pending; old rows are read-only; closing live detail releases its video view')
      await advance(15000)
      assert.equal(await cards.count(), 0)
      check('10-second expiry is independent of navigation; polling does not restore dismissed/expired popups')
      const later = await page.evaluate(() => Date.now())
      emit('alert_created', row(11, later))
      await count(1)
      await advance(3000)
      emit('alert_created', row(12, later + 3000))
      await count(2)
      await advance(3000)
      emit('alert_escalated', { ...rows.get('burst-12'), severity: 'CRITICAL' })
      await count(2)
      await advance(4500)
      await count(1)
      assert.equal(await card(12).count(), 1)
      await advance(3000)
      await count(0)
      check('staggered arrivals have separate expiry timers; updates do not extend lifetime')

      emit('alert_created', row(13, await page.evaluate(() => Date.now())))
      await count(1)
      await card(13).getByText('Chú ý', { exact: true }).waitFor()
      // Database escalation arrives before the websocket update; the user still sees WARNING.
      rows.set('burst-13', { ...rows.get('burst-13'), severity: 'CRITICAL' })
      await click(card(13).getByRole('button', { name: 'Xác nhận', exact: true }))
      await card(13).getByText('Khẩn cấp', { exact: true }).waitFor()
      await page.clock.runFor(400)
      assert.equal(rows.get('burst-13').status, 'PENDING')
      assert.equal(rows.get('burst-13').incidentId, undefined)
      await click(card(13).getByRole('button', { name: /Xác nhận/ }))
      await count(0)
      assert.equal(rows.get('burst-13').incidentId, 'fixture-incident-burst-13')
      check('stale WARNING acknowledgement updates popup to CRITICAL and requires a second explicit confirmation')

      assert.deepEqual(errors, [])
      console.log(`PASS ${viewport.width}x${viewport.height}`)
    } catch (error) {
      await page.screenshot({ path: path.join(output, `failure-${viewport.width}.png`), animations: 'disabled' })
      await fs.writeFile(path.join(output, 'failure-dom.txt'), await page.locator('body').innerText())
      throw error
    } finally { releaseWarning(); await context.close() }
  }
  report.passed = true
} catch (error) {
  report.failure = error.stack
  throw error
} finally {
  await fs.writeFile(path.join(output, 'results.json'), JSON.stringify(report, null, 2))
  await browser.close()
  await vite.close()
}
console.log(`PASS ${report.checks.length} browser checks. Fixture API/SignalR only; no database writes.`)
