import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import {
  DEFAULT_FACEBOOK_URL,
  DEFAULT_INSTAGRAM_URL,
  DEFAULT_MESSENGER_URL,
  DEFAULT_SUPPORT_PHONE_DISPLAY,
  DEFAULT_ZALO_URL,
  SUPPORT_CHANNELS,
  normalizeSupportUrl,
  resolveSupportChannels,
} from './supportChannels.js'

test('resolveSupportChannels provides valid non-placeholder Zalo and Messenger URLs by default', () => {
  const channels = resolveSupportChannels({})
  assert.equal(channels.phoneDisplay, DEFAULT_SUPPORT_PHONE_DISPLAY)
  assert.equal(channels.messengerUrl, DEFAULT_MESSENGER_URL)
  assert.equal(channels.zaloUrl, DEFAULT_ZALO_URL)
  assert.equal(channels.facebookUrl, DEFAULT_FACEBOOK_URL)
  assert.equal(channels.instagramUrl, DEFAULT_INSTAGRAM_URL)
  assert.doesNotMatch(channels.messengerUrl, /YOUR_PAGE_ID/)
  assert.doesNotMatch(channels.zaloUrl, /YOUR_PHONE/)
})

test('resolveSupportChannels honors safe HTTPS overrides and rejects unsafe or placeholder values', () => {
  const overridden = resolveSupportChannels({
    VITE_MESSENGER_LINK: 'https://m.me/custom.pool.oa',
    VITE_ZALO_LINK: 'https://zalo.me/0988888888',
  })
  assert.equal(overridden.messengerUrl, 'https://m.me/custom.pool.oa')
  assert.equal(overridden.zaloUrl, 'https://zalo.me/0988888888')

  assert.equal(normalizeSupportUrl('https://m.me/YOUR_PAGE_ID', DEFAULT_MESSENGER_URL), DEFAULT_MESSENGER_URL)
  assert.equal(normalizeSupportUrl('https://zalo.me/YOUR_PHONE', DEFAULT_ZALO_URL), DEFAULT_ZALO_URL)
  assert.equal(normalizeSupportUrl('javascript:alert(1)', DEFAULT_ZALO_URL), DEFAULT_ZALO_URL)
  assert.equal(normalizeSupportUrl('   ', DEFAULT_MESSENGER_URL), DEFAULT_MESSENGER_URL)
})

test('FloatingContact, Footer, ContactPage, and MainLayout wire SUPPORT_CHANNELS consistently without placeholders', () => {
  const floatingSource = readFileSync(new URL('./FloatingContact.jsx', import.meta.url), 'utf8')
  const footerSource = readFileSync(new URL('./Footer.jsx', import.meta.url), 'utf8')
  const contactSource = readFileSync(new URL('../../pages/ContactPage/ContactPage.jsx', import.meta.url), 'utf8')
  const mainLayoutSource = readFileSync(new URL('../../layouts/MainLayout.jsx', import.meta.url), 'utf8')

  assert.doesNotMatch(floatingSource, /YOUR_PAGE_ID|YOUR_PHONE/)
  assert.match(floatingSource, /SUPPORT_CHANNELS\.messengerUrl/)
  assert.match(floatingSource, /SUPPORT_CHANNELS\.zaloUrl/)
  assert.match(footerSource, /SUPPORT_CHANNELS\.zaloUrl/)
  assert.match(contactSource, /SUPPORT_CHANNELS\.zaloUrl/)
  assert.match(contactSource, /SUPPORT_CHANNELS\.phoneDisplay/)
  assert.match(mainLayoutSource, /<FloatingContact\s*\/>/)
  assert.equal(SUPPORT_CHANNELS.messengerUrl, 'https://www.facebook.com/messages/e2ee/t/7949322905104455')
  assert.equal(SUPPORT_CHANNELS.zaloUrl, 'https://zalo.me/0904363736')
})
