import test from 'node:test'
import assert from 'node:assert/strict'
import { hasAcceptedEntry, prepareTicketCode, shouldReconcileConfirmation } from '../src/features/tickets/services/poolAccessState.js'

test('Given an accepted entry history item When checking reconciliation Then it identifies the recorded admission', () => {
  const entries = [{ status: 'Denied' }, { status: 'Allowed', ticketId: 'ticket-1' }]

  const result = hasAcceptedEntry(entries)

  assert.equal(result, true)
})

test('Given a reserved staff payload When preparing a manual ticket code Then it rejects before lookup', () => {
  const result = prepareTicketCode('SMARTPOOL:STAFF:V1:fixture')

  assert.equal(result.accepted, false)
})

test('Given a 5xx confirmation response When classifying it Then it requires reconciliation', () => {
  const result = shouldReconcileConfirmation({ response: { status: 500 } })

  assert.equal(result, true)
})
