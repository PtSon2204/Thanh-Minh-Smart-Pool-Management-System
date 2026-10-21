import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import os from 'node:os'

// Use an installed Playwright package or the desktop's bundled browser runtime.
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright')
const baseURL = process.env.RESPONSIVE_BASE_URL || 'http://127.0.0.1:5188'
const outputDir = process.env.RESPONSIVE_OUTPUT_DIR || path.join(os.tmpdir(), 'smartpool-responsive')
const widths = (process.env.RESPONSIVE_WIDTHS || '320,375,390,600,768,1024,1440').split(',').map(Number)
const height = Number(process.env.RESPONSIVE_HEIGHT || 900)
const routes = [
  '/', '/gioi-thieu', '/bang-gia', '/lien-he', '/dang-nhap', '/dang-ky', '/quen-mat-khau', '/401', '/khong-ton-tai',
  '/ho-so', '/doi-mat-khau', '/the-cua-toi', '/lich-su-thanh-toan',
  '/admin', '/admin/doi-mat-khau', '/admin/users', '/admin/roles/permissions',
  '/admin/staff/shifts', '/admin/staff/schedule', '/admin/staff/profiles', '/admin/staff/attendance', '/admin/staff/attendance/qr',
  '/admin/tickets/types', '/admin/tickets/pos', '/admin/tickets/scanQR', '/admin/coupons',
  '/admin/equipments/list-equipments', '/admin/equipments/maintenance', '/admin/equipments/maintenance-history',
  '/admin/services', '/admin/inventory', '/admin/rentals', '/admin/payments/invoices', '/admin/notifications',
  '/admin/statistics/ticket-revenue', '/admin/statistics/monthly-tickets', '/admin/statistics/user-count',
  '/admin/statistics/incidents', '/admin/statistics/operations-costs',
]
const selectedRoutes = routes.filter(route => !process.env.RESPONSIVE_ROUTES || process.env.RESPONSIVE_ROUTES.split(',').includes(route))
const ticketTypes = [
  { id: 'ticket-month', name: 'Vé tháng người lớn', price: 500000, ticketCategory: 'VE_THANG', durationDays: 30, isActive: true },
  { id: 'ticket-day', name: 'Vé lượt người lớn', price: 50000, ticketCategory: 'VE_LUOT', isActive: true },
]
const profile = { id: 'test-user', fullName: 'Nguyễn Thị Minh Anh', phone: '0912345678', email: 'minhanh@example.test', address: 'Hà Nội', dateOfBirth: '1995-01-01', gender: 'FEMALE' }
const pageData = { items: [], totalCount: 0, pageIndex: 1, pageSize: 10, unreadCount: 0 }
const populatedPage = items => ({ ...pageData, items, totalCount: items.length })

await mkdir(outputDir, { recursive: true })
const browser = await chromium.launch({ headless: true, channel: process.env.RESPONSIVE_BROWSER || 'msedge' })
const results = []
let activePage
try {
  for (const width of widths) {
    const context = await browser.newContext({ viewport: { width, height }, reducedMotion: 'reduce' })
    await context.addInitScript(() => {
      if (window.top !== window || !['localhost', '127.0.0.1'].includes(location.hostname)) return
      if (sessionStorage.getItem('thanh-minh-auth-session')) return
      sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: {
        id: 'test-user', username: 'responsive-preview', role: 'ADMIN', accessToken: 'local-layout-fixture',
        expiresAtUtc: '2099-01-01T00:00:00Z',
      } }, version: 0 }))
    })
    // Never send test reads or mutations to a real backend.
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url())
      let data = pageData
      if (url.pathname.endsWith('/profiles/me')) data = profile
      else if (url.pathname.endsWith('/tickets/checkout') || url.pathname.includes('/tickets/me/orders/')) data = {
        orderId: 'layout-order-12345678', status: 'PENDING', subtotal: 500000, discountAmount: 0, finalAmount: 500000,
        paidAmount: 0, remainingAmount: 500000, serverTimeUtc: new Date().toISOString(), expiresAtUtc: new Date(Date.now() + 300000).toISOString(),
        items: [{ ticketTypeId: 'ticket-month', name: 'Vé tháng người lớn', quantity: 1, unitPrice: 500000, lineTotal: 500000 }],
      }
      else if (url.pathname.endsWith('/tickets/me')) data = [{ id: 'ticket-1', ticketTypeName: 'Vé tháng người lớn', ticketCategory: 'VE_THANG', qrCode: '123456', expiryDate: '2099-01-01', status: 'ACTIVE' }]
      else if (url.pathname.includes('/ticket-types')) data = { ...pageData, items: ticketTypes, totalCount: ticketTypes.length }
      else if (url.pathname.includes('/reports/')) data = {}
      else if (url.pathname.endsWith('/roles')) data = [{ id: 'role-1', name: 'LIFEGUARD' }]
      else if (url.pathname.endsWith('/staffs/schedule/employees') || url.pathname.endsWith('/equipments/options')) data = []
      else if (url.pathname.includes('/permissions/')) data = {}
      else if (url.pathname.endsWith('/maintenance-schedules/options')) data = { equipments: [], assignees: [] }
      else if (url.pathname.endsWith('/shifts')) data = { ...pageData, items: [{ id: 'shift-1', name: 'Ca sáng', startTime: '06:00:00', endTime: '12:00:00', isActive: true }], totalCount: 1 }
      else if (url.pathname.endsWith('/users')) data = populatedPage([{ ...profile, username: 'minhanh', role: 'CUSTOMER', status: 'ACTIVE' }])
      else if (url.pathname.endsWith('/services')) data = populatedPage([{ id: 'service-1', name: 'Phao bơi trẻ em và phụ kiện bơi lội', price: 20000, stockQuantity: 50, isActive: true }])
      else if (url.pathname.endsWith('/equipments')) data = populatedPage([{ id: 'equipment-1', name: 'Máy bơm lọc tuần hoàn hồ bơi', category: 'Máy bơm', location: 'Khu vực hồ bơi người lớn', status: 'Active' }])
      else if (url.pathname.endsWith('/rentals')) data = populatedPage([{ orderId: 'order-12345678', productId: 'service-1', productName: 'Phao bơi trẻ em', quantity: 2, returnedQuantity: 0, outstandingQuantity: 2, depositAmount: 100000, rentTime: '2026-10-21T07:00:00Z', customerName: profile.fullName }])
      await route.fulfill({ json: data })
    })
    await context.route('**/hubs/**', route => route.abort())
    await context.route(/https:\/\/(?!127\.0\.0\.1|localhost)/, route => route.abort())
    const page = await context.newPage()
    activePage = page
    page.setDefaultTimeout(10000)
    const errors = []
    page.on('pageerror', error => errors.push(error.message))
    const visit = async route => {
      errors.length = 0
      await page.goto(new URL(route, baseURL).href, { waitUntil: 'domcontentloaded' })
      await page.locator('#root > *').first().waitFor()
      await page.waitForTimeout(300)
    }
    const recordLayout = async route => {
      if (await page.getByRole('dialog').count()) await page.waitForTimeout(300)
      const layout = await page.evaluate(() => {
        const viewport = document.documentElement.clientWidth
        const overflow = [...document.querySelectorAll('body *')].flatMap(el => {
          const rect = el.getBoundingClientRect()
          const style = getComputedStyle(el)
          if (!rect.width || !rect.height || style.visibility === 'hidden' || style.display === 'none') return []
          if (rect.right <= viewport + 1 && rect.left >= -1) return []
          // Wide tables and decorative tracks are intentionally clipped/scrollable within their own containers.
          let parent = el.parentElement
          while (parent && parent !== document.body && parent !== document.documentElement) {
            const parentStyle = getComputedStyle(parent)
            if (['auto', 'scroll', 'hidden', 'clip'].includes(parentStyle.overflowX)) {
              const bounds = parent.getBoundingClientRect()
              if (bounds.right <= viewport + 1 && bounds.left >= -1) return []
            }
            parent = parent.parentElement
          }
          return [{ element: `${el.tagName.toLowerCase()}.${String(el.className).replace(/\s+/g, '.')}`.slice(0, 150), left: Math.round(rect.left), right: Math.round(rect.right) }]
        })
        overflow.push(...[...document.querySelectorAll('.ant-picker-dropdown')].flatMap(el => {
          const rect = el.getBoundingClientRect()
          if (!rect.width || !rect.height || getComputedStyle(el).visibility === 'hidden') return []
          return rect.top < -1 || rect.bottom > innerHeight + 1 ? [{ element: 'date-picker-popup', top: Math.round(rect.top), bottom: Math.round(rect.bottom) }] : []
        }))
        const appError = document.body.innerText.includes('Unexpected Application Error!')
        const invisibleDialogs = [...document.querySelectorAll('[role="dialog"][aria-modal="true"]')].filter(el => {
          const rect = el.getBoundingClientRect()
          return rect.width && rect.height && getComputedStyle(el).visibility !== 'hidden' && getComputedStyle(el).opacity === '0'
        }).map(el => el.className)
        return { viewport, scrollWidth: document.documentElement.scrollWidth, overflow: overflow.slice(0, 12), invisibleDialogs, appError, ...(appError ? { errorText: document.body.innerText.slice(0, 800) } : {}) }
      })
      const result = { width, height, route, ...layout, errors: [...errors] }
      results.push(result)
      if (layout.overflow.length || layout.invisibleDialogs.length || layout.appError || errors.length) console.log(JSON.stringify(result))
    }
    for (const route of selectedRoutes) {
      await visit(route)
      await recordLayout(route)
      if ([375, 1440].includes(width) && ['/', '/bang-gia', '/admin', '/admin/tickets/pos', '/admin/statistics/user-count'].includes(route)) {
        await page.screenshot({ path: path.join(outputDir, `${width}-${route.replaceAll('/', '-') || 'home'}.png`) })
      }
    }
    console.log(`Checked route layouts at ${width}px`)
    if (!process.env.RESPONSIVE_SKIP_INTERACTIONS) {
      if (width <= 1024) {
        await visit('/')
        await page.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click()
        await page.locator('#mobile-navigation').waitFor({ state: 'visible' })
        await recordLayout('/#mobile-menu')
        await page.locator('#mobile-navigation').getByRole('link', { name: 'Bảng giá vé', exact: true }).click()
        await page.locator('#mobile-navigation').waitFor({ state: 'hidden' })
        assert.equal(new URL(page.url()).pathname, '/bang-gia')
      }
      if (width < 768) {
        await visit('/admin')
        await page.getByRole('button', { name: 'Mở menu quản trị', exact: true }).click()
        await page.locator('.admin-navigation-drawer').waitFor({ state: 'visible' })
        await recordLayout('/admin#mobile-menu')
        await page.locator('.admin-navigation-drawer').getByRole('link', { name: 'Quản lý người dùng', exact: true }).click()
        await page.locator('.admin-navigation-drawer .ant-drawer-content-wrapper').waitFor({ state: 'hidden' })
        assert.equal(new URL(page.url()).pathname, '/admin/users')
      }
      for (const [route, button] of [
        ['/admin/tickets/types', 'Thêm loại vé'], ['/admin/coupons', 'Phát hành mã mới'],
        ['/admin/staff/shifts', 'Tạo ca'], ['/admin/equipments/list-equipments', 'Thêm thiết bị'],
        ['/admin/equipments/maintenance', 'Lập lịch bảo trì'], ['/admin/services', 'Thêm đồ cho thuê'],
        ['/admin/rentals', 'Tạo đơn thuê'],
      ]) {
        await visit(route)
        await page.getByRole('button', { name: new RegExp(`${button}$`) }).first().click()
        await page.getByRole('dialog').waitFor({ state: 'visible' })
        await page.waitForTimeout(500)
        await recordLayout(`${route}#form`)
        if (width === 375 && route === '/admin/coupons') await page.screenshot({ path: path.join(outputDir, '375-voucher-form.png'), animations: 'disabled' })
        if (route === '/admin/coupons') {
          await page.locator('.ant-modal .ant-picker input').first().click()
          await page.waitForTimeout(500)
          await recordLayout(`${route}#date-time-picker`)
          if (width === 375) await page.screenshot({ path: path.join(outputDir, '375-voucher-datepicker.png'), animations: 'disabled' })
        }
      }
      await visit('/admin/tickets/pos')
      await page.locator('.pos-catalog').getByText('Vé lượt người lớn', { exact: true }).click()
      await page.getByRole('button', { name: 'THANH TOÁN', exact: true }).click()
      await page.getByRole('dialog').waitFor({ state: 'visible' })
      await recordLayout('/admin/tickets/pos#payment-method')
      await visit('/admin/notifications')
      await page.getByRole('tab', { name: /Gửi thông báo tự động qua Email/ }).click()
      await page.waitForTimeout(300)
      await recordLayout('/admin/notifications#email')
      await visit('/admin/payments/invoices')
      await page.locator('.payment-filter-card .ant-picker input').first().click()
      await page.waitForTimeout(500)
      await recordLayout('/admin/payments/invoices#date-range-picker')
      await page.evaluate(() => {
        const key = 'thanh-minh-auth-session'
        const auth = JSON.parse(sessionStorage.getItem(key))
        auth.state.session.role = 'CUSTOMER'
        sessionStorage.setItem(key, JSON.stringify(auth))
      })
      await visit('/bang-gia')
      await page.getByRole('button', { name: /Thêm giỏ hàng/ }).first().click()
      await page.getByRole('dialog').waitFor({ state: 'visible' })
      await recordLayout('/bang-gia#customer-cart')
      if (width === 375) await page.screenshot({ path: path.join(outputDir, '375-customer-cart.png') })
      await page.locator('.checkout-btn').click()
      await page.locator('.online-ticket-payment-modal').waitFor({ state: 'visible' })
      await page.waitForTimeout(500)
      await recordLayout('/bang-gia#online-payment')
      if (width === 375) await page.screenshot({ path: path.join(outputDir, '375-online-payment.png'), animations: 'disabled' })
      await page.evaluate(() => {
        const key = 'thanh-minh-auth-session'
        const auth = JSON.parse(sessionStorage.getItem(key))
        auth.state.session.role = 'SALES'
        sessionStorage.setItem(key, JSON.stringify(auth))
      })
      await visit('/admin/staff/attendance/scan')
      await recordLayout('/admin/staff/attendance/scan#staff')
      await page.evaluate(() => sessionStorage.setItem('thanh-minh-auth-session', JSON.stringify({ state: { session: null }, version: 0 })))
      await visit('/')
      await recordLayout('/#guest')
      if (width <= 1024) {
        await page.getByRole('button', { name: 'Mở menu điều hướng', exact: true }).click()
        await page.locator('#mobile-navigation').getByRole('link', { name: 'Đăng nhập', exact: true }).waitFor({ state: 'visible' })
        await page.locator('#mobile-navigation').getByRole('link', { name: 'Đăng ký', exact: true }).waitFor({ state: 'visible' })
        await recordLayout('/#guest-menu')
      }
    }
    await context.close()
    console.log(`Checked ${selectedRoutes.length} routes and requested interactions at ${width}px`)
  }
} catch (error) {
  if (activePage && !activePage.isClosed()) {
    await activePage.screenshot({ path: path.join(outputDir, 'failure.png') })
    console.error('Failed on', activePage.url(), await activePage.locator('body').innerText().then(text => text.slice(0, 1200)))
  }
  throw error
} finally {
  await browser.close()
  await writeFile(path.join(outputDir, 'results.json'), JSON.stringify(results, null, 2))
}
const failures = results.filter(result => result.overflow.length || result.invisibleDialogs.length || result.appError || result.errors.length || result.scrollWidth > result.viewport + 1)
console.log(`${results.length} route/viewport checks; ${failures.length} failures. Report: ${outputDir}`)
assert.equal(failures.length, 0, 'Pages must fit the viewport without a render error')
