import assert from 'node:assert/strict'
import test from 'node:test'
import { hasAcceptedEntry, shouldReconcileConfirmation, vietnamDate } from './poolAccessState.js'

test('accepted admission history matches status casing and API field casing', () => {
  assert.equal(hasAcceptedEntry([{ Status: 'ALLOWED' }]), true)
  assert.equal(hasAcceptedEntry([{ status: 'Denied' }]), false)
})

test('Vietnam date changes at local midnight, not UTC midnight', () => {
  assert.equal(vietnamDate(new Date('2026-10-04T16:59:59.000Z')), '2026-10-04')
  assert.equal(vietnamDate(new Date('2026-10-04T17:00:00.000Z')), '2026-10-05')
})

test('confirmation with no response status requires reconciliation', () => {
  assert.equal(shouldReconcileConfirmation(new Error('connection closed')), true)
  assert.equal(shouldReconcileConfirmation({ response: { status: 409 } }), true)
  assert.equal(shouldReconcileConfirmation({ response: { status: 200 } }), false)
})
