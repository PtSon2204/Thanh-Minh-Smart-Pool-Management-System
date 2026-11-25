export const DEFAULT_SUPPORT_PHONE_DISPLAY = '0901 234 567'
export const DEFAULT_SUPPORT_PHONE_DIGITS = '0901234567'
export const DEFAULT_ZALO_PHONE = '0904363736'
export const DEFAULT_MESSENGER_URL = 'https://www.facebook.com/messages/e2ee/t/7949322905104455'
export const DEFAULT_ZALO_URL = 'https://zalo.me/0904363736'
export const DEFAULT_FACEBOOK_URL = 'https://facebook.com/thanhminhsmartpool'
export const DEFAULT_INSTAGRAM_URL = 'https://instagram.com/thanhminhsmartpool'

const PLACEHOLDER_TOKENS = ['YOUR_PAGE_ID', 'YOUR_PHONE']

export function normalizeSupportUrl(rawUrl, fallbackUrl) {
  if (typeof rawUrl !== 'string') return fallbackUrl
  const trimmed = rawUrl.trim()
  if (!trimmed || PLACEHOLDER_TOKENS.some((token) => trimmed.includes(token))) {
    return fallbackUrl
  }
  try {
    const parsed = new URL(trimmed)
    if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
      return fallbackUrl
    }
    return parsed.href
  } catch {
    return fallbackUrl
  }
}

export function resolveSupportChannels(env) {
  const sourceEnv = env ?? (typeof import.meta !== 'undefined' && import.meta.env ? import.meta.env : {})
  return {
    phoneDisplay: DEFAULT_SUPPORT_PHONE_DISPLAY,
    phoneDigits: DEFAULT_SUPPORT_PHONE_DIGITS,
    messengerUrl: normalizeSupportUrl(sourceEnv.VITE_MESSENGER_LINK, DEFAULT_MESSENGER_URL),
    zaloUrl: normalizeSupportUrl(sourceEnv.VITE_ZALO_LINK, DEFAULT_ZALO_URL),
    facebookUrl: normalizeSupportUrl(sourceEnv.VITE_FACEBOOK_LINK, DEFAULT_FACEBOOK_URL),
    instagramUrl: normalizeSupportUrl(sourceEnv.VITE_INSTAGRAM_LINK, DEFAULT_INSTAGRAM_URL),
  }
}

export const SUPPORT_CHANNELS = resolveSupportChannels()
