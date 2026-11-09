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
const playwrightModule = process.env.SMARTPOOL_PLAYWRIGHT_MODULE
if (!playwrightModule) throw new Error('Set SMARTPOOL_PLAYWRIGHT_MODULE to an installed playwright-core index.mjs')
const { chromium } = await import(pathToFileURL(playwrightModule).href)
const vite = await createServer({ root: frontend, server: { host: '127.0.0.1', port: 0 } })
await vite.listen()
const origin = vite.resolvedUrls.local[0]
let browser
try {
  browser = await chromium.launch({ channel: 'chrome', headless: true })
  const errors = []
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } })
  await context.addInitScript(({ token, id }) => {
    sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
      id, username: 'integration-admin', role: 'ADMIN', roles: ['ADMIN'], accessToken: token, expiresAtUtc: new Date(Date.now() + 120000).toISOString(),
    } }, version: 0 }))
  }, { token: settings.adminToken, id: settings.adminId })
  const page = await context.newPage()
  const safetySockets = []
  page.on('websocket', socket => { if (socket.url().includes('/hubs/notifications')) safetySockets.push(socket) })
  page.on('pageerror', error => errors.push(error.message))
  await page.goto(new URL('/admin/safety/list-camera', origin).href)
  await page.getByRole('heading', { name: 'Danh sách camera', exact: true }).waitFor()
  await page.getByText('Camera A', { exact: true }).waitFor()
  await page.getByText('Camera hiện không khả dụng', { exact: true }).waitFor()
  await page.getByLabel('Tìm camera').fill('nonexistent-camera-for-integration-test')
  await page.getByText('Không tìm thấy camera phù hợp', { exact: true }).waitFor()
  await page.getByRole('button', { name: 'Đặt lại', exact: true }).click()
  await page.getByText('Camera A', { exact: true }).waitFor()
  await page.screenshot({ path: path.join(settings.root, 'backend/.test-runtime/camera-list-ui.png'), fullPage: true })
  await page.getByRole('link', { name: 'Phân tích Video', exact: true }).click()
  await page.getByRole('heading', { name: 'Phân tích Video', exact: true }).waitFor()
  await page.getByText('Dịch vụ phân tích video chưa sẵn sàng', { exact: true }).waitFor()
  if (!await page.getByRole('button', { name: 'Bắt đầu phân tích', exact: true }).isDisabled()) throw new Error('Upload must be disabled while AI is unconfigured')
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: path.join(settings.root, 'backend/.test-runtime/video-analysis-mobile-ui.png'), fullPage: true })
  await page.getByRole('button', { name: 'Menu tài khoản', exact: true }).click()
  await page.getByText('Đăng xuất', { exact: true }).click()
  await page.waitForURL('**/dang-nhap')
  await Promise.all(safetySockets.filter(socket => !socket.isClosed()).map(socket => new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Safety socket remained connected after logout')), 5000)
    socket.on('close', () => { clearTimeout(timer); resolve() })
  })))
  const guard = await browser.newContext()
  await guard.addInitScript(({ token, id }) => sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
    id, username: 'integration-guard', role: 'LIFEGUARD', roles: ['LIFEGUARD'], accessToken: token, expiresAtUtc: new Date(Date.now() + 120000).toISOString(),
  } }, version: 0 })), { token: settings.guardToken, id: settings.guardId })
  const guardPage = await guard.newPage()
  await guardPage.goto(new URL('/admin/safety/list-camera', origin).href)
  await guardPage.waitForURL('**/401')
  await guardPage.goto(new URL('/admin/safety/camera-alerts', origin).href)
  await guardPage.getByText('Cảnh báo camera LIVE', { exact: true }).waitFor()
  if (await guardPage.getByRole('link', { name: 'Danh sách camera', exact: true }).count()) throw new Error('Lifeguard must not gain camera-list permission')
  if (errors.length) throw new Error(errors.join('\n'))
  console.log('Headless UI passed: camera list/search/reset, video page/disabled state, mobile, lifeguard authorization, logout closes safety sockets.')
} finally {
  await browser?.close()
  await vite.close()
}
