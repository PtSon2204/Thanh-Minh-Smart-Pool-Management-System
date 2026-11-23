import assert from 'node:assert/strict'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

process.env.RAYON_NUM_THREADS = process.env.RAYON_NUM_THREADS || '1'

const scriptDir = path.dirname(fileURLToPath(import.meta.url))
const frontend = path.resolve(scriptDir, '..')

const { createServer } = await import(pathToFileURL(path.join(frontend, 'node_modules/vite/dist/node/index.js')).href)
const playwrightEntry = process.env.SMARTPOOL_PLAYWRIGHT_MODULE || path.join(frontend, 'node_modules/playwright-core/index.mjs')
const { chromium } = await import(pathToFileURL(playwrightEntry).href)

const HARNESS_ENTRY_ID = 'virtual:customer-support-it-entry.jsx'
const HARNESS_RESOLVED_ID = `\0${HARNESS_ENTRY_ID}`

function customerSupportTestHarnessPlugin() {
  return {
    name: 'customer-support-it-harness',
    configureServer(server) {
      server.middlewares.use('/support-it.html', async (req, res) => {
        const html = await server.transformIndexHtml(
          req.originalUrl || '/support-it.html',
          `<!doctype html>
<html lang="vi">
  <head><meta charset="UTF-8" /><title>Customer Support UI Integration Test</title></head>
  <body>
    <div id="root"></div>
    <script type="module" src="/@id/__x00__${HARNESS_ENTRY_ID}"></script>
  </body>
</html>`,
        )
        res.statusCode = 200
        res.setHeader('Content-Type', 'text/html')
        res.end(html)
      })
    },
    resolveId(id) {
      if (id === HARNESS_ENTRY_ID || id.endsWith(HARNESS_ENTRY_ID)) return HARNESS_RESOLVED_ID
    },
    load(id) {
      if (id !== HARNESS_RESOLVED_ID) return null
      return `
import React from 'react'
import { createRoot } from 'react-dom/client'
import { MemoryRouter, Route, Routes, Link } from 'react-router-dom'
import FloatingContact from '/src/components/shared/FloatingContact.jsx'
import Footer from '/src/components/shared/Footer.jsx'
import ContactPage from '/src/pages/ContactPage/ContactPage.jsx'

function SupportHarnessApp() {
  return React.createElement(
    MemoryRouter,
    { initialEntries: ['/'] },
    React.createElement(
      'nav',
      { style: { padding: 12 } },
      React.createElement(Link, { to: '/' }, 'Trang chủ'),
      ' | ',
      React.createElement(Link, { to: '/lien-he' }, 'Liên hệ'),
    ),
    React.createElement(
      Routes,
      null,
      React.createElement(Route, {
        path: '/',
        element: React.createElement(
          'main',
          { style: { minHeight: 600, padding: 24 } },
          React.createElement('h1', null, 'Trang chủ'),
        ),
      }),
      React.createElement(Route, {
        path: '/lien-he',
        element: React.createElement(ContactPage),
      }),
    ),
    React.createElement(FloatingContact),
    React.createElement(Footer),
  )
}

createRoot(document.getElementById('root')).render(React.createElement(SupportHarnessApp))
`
    },
  }
}

async function launchBrowser() {
  for (const channel of ['chrome', 'msedge', undefined]) {
    try {
      return await chromium.launch({ ...(channel ? { channel } : {}), headless: true })
    } catch {
      // Try next installed browser channel
    }
  }
  throw new Error('No headless Chromium/Chrome/Edge browser executable found for Playwright.')
}

const vite = await createServer({
  root: frontend,
  configFile: false,
  plugins: [
    (await import(pathToFileURL(path.join(frontend, 'node_modules/@vitejs/plugin-react/dist/index.js')).href)).default(),
    customerSupportTestHarnessPlugin(),
  ],
  optimizeDeps: {
    noDiscovery: true,
    include: ['react', 'react-dom', 'react-dom/client', 'react-router-dom'],
  },
  server: { host: '127.0.0.1', port: 0 },
})

await vite.listen()
const origin = vite.resolvedUrls.local[0]
let browser

try {
  browser = await launchBrowser()
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } })

  // Intercept external support channel navigations offline so clicks never depend on public internet
  await context.route(/^https:\/\/(m\.me|zalo\.me|(www\.)?facebook\.com|instagram\.com|www\.google\.com\/maps)/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/html; charset=utf-8',
      body: `<!doctype html><html><body><h1>External Support Channel</h1><p>${route.request().url()}</p></body></html>`,
    })
  })

  const pageErrors = []
  const page = await context.newPage()
  page.on('pageerror', (err) => pageErrors.push(err.message))

  // 1. Check FloatingContact widget visibility, attributes & popup clicks on Home route (/)
  await page.goto(new URL('/support-it.html', origin).href, { waitUntil: 'domcontentloaded' })
  const messengerBtn = page.locator('.floating-contacts a[aria-label="Messenger"]')
  const zaloBtn = page.locator('.floating-contacts a[aria-label="Zalo"]')

  await messengerBtn.waitFor({ state: 'visible' })
  await zaloBtn.waitFor({ state: 'visible' })

  assert.equal(await messengerBtn.getAttribute('href'), 'https://www.facebook.com/messages/e2ee/t/7949322905104455')
  assert.equal(await messengerBtn.getAttribute('target'), '_blank')
  assert.match((await messengerBtn.getAttribute('rel')) ?? '', /noopener/)
  assert.equal(await messengerBtn.getAttribute('data-tooltip'), 'Nhắn tin Messenger')

  assert.equal(await zaloBtn.getAttribute('href'), 'https://zalo.me/0904363736')
  assert.equal(await zaloBtn.getAttribute('target'), '_blank')
  assert.match((await zaloBtn.getAttribute('rel')) ?? '', /noopener/)
  assert.equal(await zaloBtn.getAttribute('data-tooltip'), 'Chat Zalo')

  const [messengerPopup] = await Promise.all([
    page.waitForEvent('popup'),
    messengerBtn.click(),
  ])
  await messengerPopup.waitForLoadState('domcontentloaded')
  assert.equal(messengerPopup.url(), 'https://www.facebook.com/messages/e2ee/t/7949322905104455')
  await messengerPopup.close()

  const [zaloPopup] = await Promise.all([
    page.waitForEvent('popup'),
    zaloBtn.click(),
  ])
  await zaloPopup.waitForLoadState('domcontentloaded')
  assert.equal(zaloPopup.url(), 'https://zalo.me/0904363736')
  await zaloPopup.close()

  // 2. Navigate to ContactPage (/lien-he) and verify Contact info + Social channels + Footer links
  await page.getByRole('link', { name: 'Liên hệ', exact: true }).click()
  await page.getByRole('heading', { name: 'Liên hệ với chúng tôi' }).waitFor({ state: 'visible' })
  await page.getByText('0901 234 567', { exact: true }).waitFor({ state: 'visible' })

  const contactZaloPill = page.locator('.social-links a', { hasText: 'Zalo OA' })
  const contactMessengerPill = page.locator('.social-links a', { hasText: 'Messenger' })
  assert.equal(await contactZaloPill.getAttribute('href'), 'https://zalo.me/0904363736')
  assert.equal(await contactZaloPill.getAttribute('target'), '_blank')
  assert.equal(await contactMessengerPill.getAttribute('href'), 'https://www.facebook.com/messages/e2ee/t/7949322905104455')
  assert.equal(await contactMessengerPill.getAttribute('target'), '_blank')

  const [contactZaloPopup] = await Promise.all([
    page.waitForEvent('popup'),
    contactZaloPill.click(),
  ])
  await contactZaloPopup.waitForLoadState('domcontentloaded')
  assert.equal(contactZaloPopup.url(), 'https://zalo.me/0904363736')
  await contactZaloPopup.close()

  const footerZalo = page.locator('.footer-social a[aria-label="Zalo"]')
  const footerMessenger = page.locator('.footer-social a[aria-label="Messenger"]')
  assert.equal(await footerZalo.getAttribute('href'), 'https://zalo.me/0904363736')
  assert.equal(await footerMessenger.getAttribute('href'), 'https://www.facebook.com/messages/e2ee/t/7949322905104455')

  // 3. Mobile responsiveness check (390x844): floating support buttons stay inside viewport
  await page.setViewportSize({ width: 390, height: 844 })
  const mobileMessengerBox = await messengerBtn.boundingBox()
  const mobileZaloBox = await zaloBtn.boundingBox()
  assert.ok(mobileMessengerBox && mobileMessengerBox.x >= 0 && mobileMessengerBox.x + mobileMessengerBox.width <= 390)
  assert.ok(mobileZaloBox && mobileZaloBox.x >= 0 && mobileZaloBox.x + mobileZaloBox.width <= 390)
  assert.ok(mobileZaloBox.y >= mobileMessengerBox.y + mobileMessengerBox.height)

  assert.deepEqual(pageErrors, [])
  console.log('Customer Support UI Integration Tests PASSED: FloatingContact (desktop + mobile), Messenger/Zalo popups, ContactPage, Footer.')
} catch (error) {
  console.error('Customer Support UI Integration Test FAILED:', error)
  process.exitCode = 1
} finally {
  await browser?.close()
  await vite.close()
}
