import test from 'node:test'
import assert from 'node:assert/strict'
import { normalizeCameraSearch, validateCameraSearch } from './cameraSearch.js'

test('camera search ignores whitespace and accepts Vietnamese names and locations', () => {
  for (const value of ['Camera demo', 'phía bên phải', 'mét 7', 'ca', '12']) {
    assert.equal(validateCameraSearch(value), null)
  }
  assert.equal(normalizeCameraSearch('  ca\tmera \n 2 '), 'camera2')
})

test('single characters and punctuation do not count toward the minimum search length', () => {
  for (const value of ['t', ' t ', '7', ' t ! ', '%_', '!?']) {
    assert.ok(validateCameraSearch(value), value)
  }
})

test('empty and whitespace-only submissions require a camera name or location', () => {
  for (const value of ['', ' ', ' \t\n ', '\u00a0\u3000'])
    assert.equal(validateCameraSearch(value), 'Vui lòng nhập tên hoặc vị trí camera trước khi tìm kiếm.')
  assert.equal(normalizeCameraSearch(' \t\n '), '')
})

test('the 200-character limit excludes whitespace', () => {
  assert.equal(validateCameraSearch('a '.repeat(200)), null)
  assert.ok(validateCameraSearch('a'.repeat(201)))
})
