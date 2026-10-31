import { useCallback, useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '../../auth/store/authStore'
import { INVENTORY_HISTORY_QUERY_KEY, SERVICES_QUERY_KEY } from '../../services/hooks/useServices'
import rentalService from '../services/rentalService'
import { RENTAL_DETAILS_QUERY_KEY, RENTALS_QUERY_KEY } from './useRentals'
import {
  createRentalPaymentClock,
  getRentalPaymentSecondsLeft,
  isRentalPaymentWaiting,
  readRentalPaymentIntent,
  saveRentalPaymentIntent,
} from '../state/rentalPaymentState'
import { isRentalRequestOutcomeUncertain } from '../utils/rentalRequests'

export const RENTAL_PAYMENT_QUERY_KEY = ['rental-payment']
export const RENTAL_PAYMENTS_QUERY_KEY = ['rental-payments']

function freezeQrRequest(payload) {
  return Object.freeze({ ...payload, items: Object.freeze(payload.items.map((item) => Object.freeze({ ...item }))) })
}

function restorePaymentState(staffId) {
  const restored = readRentalPaymentIntent(staffId)
  const requestPayload = restored?.requestPayload ? freezeQrRequest(restored.requestPayload) : null
  const unresolvedRequestId = !restored?.paymentId && restored?.requestId ? restored.requestId : null
  return {
    staffId,
    activePaymentId: restored?.paymentId || unresolvedRequestId || null,
    requestPayload,
    createUncertain: Boolean(unresolvedRequestId && requestPayload),
  }
}

export function useRentalPaymentQueue(params, enabled = true) {
  return useQuery({
    queryKey: [...RENTAL_PAYMENTS_QUERY_KEY, params],
    queryFn: () => rentalService.getQrPayments(params),
    enabled,
  })
}

export function useRentalPayment() {
  const staffId = useAuthStore((state) => state.session?.id)
  const queryClient = useQueryClient()
  const [restoredState, setRestoredState] = useState(() => restorePaymentState(staffId))
  const [activePaymentId, setActivePaymentId] = useState(restoredState.activePaymentId)
  const [open, setOpen] = useState(Boolean(restoredState.activePaymentId))
  const [now, setNow] = useState(Date.now)
  const [restoredCreateUncertain, setRestoredCreateUncertain] = useState(restoredState.createUncertain)
  const [clock, setClock] = useState(null)
  const frozenCreate = useRef(restoredState.requestPayload)
  const frozenIssue = useRef(null)
  const frozenCancel = useRef(null)
  const createVersion = useRef(0)

  if (restoredState.staffId !== staffId) {
    const next = restorePaymentState(staffId)
    setRestoredState(next)
    setActivePaymentId(next.activePaymentId)
    setOpen(Boolean(next.activePaymentId))
    setRestoredCreateUncertain(next.createUncertain)
    setClock(null)
  }

  useEffect(() => {
    frozenCreate.current = restoredState.requestPayload
    frozenIssue.current = null
    frozenCancel.current = null
  }, [restoredState])

  const paymentQuery = useQuery({
    queryKey: [...RENTAL_PAYMENT_QUERY_KEY, staffId, activePaymentId],
    queryFn: async ({ signal }) => {
      const data = await rentalService.getQrPayment(activePaymentId, signal)
      if (useAuthStore.getState().session?.id === staffId && !signal.aborted) {
        setClock(createRentalPaymentClock(data))
      }
      return data
    },
    enabled: Boolean(staffId && activePaymentId),
    refetchInterval: (query) => isRentalPaymentWaiting(query.state.data?.status) ? 3000 : false,
    refetchOnWindowFocus: true,
    retry: (attempt, error) => error.response?.status !== 404 && error.response?.status !== 403 && attempt < 2,
  })

  if (restoredCreateUncertain && paymentQuery.data?.id === activePaymentId) {
    setRestoredCreateUncertain(false)
  }

  useEffect(() => {
    const restored = readRentalPaymentIntent(staffId)
    if (!paymentQuery.data || paymentQuery.data.id !== activePaymentId || restored?.paymentId) return
    if (restored?.requestId !== activePaymentId) return
    saveRentalPaymentIntent(staffId, { requestId: activePaymentId, paymentId: paymentQuery.data.id, requestPayload: null })
    frozenCreate.current = null
  }, [activePaymentId, paymentQuery.data, staffId])

  useEffect(() => {
    if (!activePaymentId) return undefined
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [activePaymentId])

  const createMutation = useMutation({ mutationFn: rentalService.createQrPayment, retry: false })
  const issueMutation = useMutation({ mutationFn: rentalService.issueQrPayment, retry: false })
  const cancelMutation = useMutation({ mutationFn: rentalService.cancelQrPayment, retry: false })

  const openPayment = useCallback((paymentId, requestId = null) => {
    if (!staffId || !paymentId) return
    saveRentalPaymentIntent(staffId, { requestId, paymentId })
    setActivePaymentId(paymentId)
    setOpen(true)
  }, [staffId])

  const startQr = useCallback(async (payload) => {
    if (!staffId) throw new Error('Phiên nhân viên đã hết hạn. Vui lòng đăng nhập lại.')
    if (activePaymentId && !frozenCreate.current) {
      setOpen(true)
      await paymentQuery.refetch()
      return paymentQuery.data
    }
    const frozen = frozenCreate.current || freezeQrRequest(payload)
    frozenCreate.current = frozen
    const version = ++createVersion.current
    saveRentalPaymentIntent(staffId, { requestId: frozen.requestId, paymentId: null, requestPayload: frozen })
    createMutation.reset()
    try {
      const data = await createMutation.mutateAsync(frozen)
      if (version !== createVersion.current || useAuthStore.getState().session?.id !== staffId) return data
      saveRentalPaymentIntent(staffId, { requestId: frozen.requestId, paymentId: data.id, requestPayload: null })
      queryClient.setQueryData([...RENTAL_PAYMENT_QUERY_KEY, staffId, data.id], data)
      setClock(createRentalPaymentClock(data))
      setActivePaymentId(data.id)
      setOpen(true)
      frozenCreate.current = null
      setRestoredCreateUncertain(false)
      return data
    } catch (error) {
      if (!isRentalRequestOutcomeUncertain(error)) {
        frozenCreate.current = null
        saveRentalPaymentIntent(staffId, null)
        setRestoredCreateUncertain(false)
      }
      throw error
    }
  }, [activePaymentId, createMutation, paymentQuery, queryClient, staffId])

  const retryCreate = useCallback(() => frozenCreate.current ? startQr(frozenCreate.current) : Promise.resolve(null), [startQr])

  const issue = useCallback(async () => {
    if (!staffId || !activePaymentId) return null
    const paymentId = frozenIssue.current || activePaymentId
    frozenIssue.current = paymentId
    issueMutation.reset()
    try {
      const issuedResult = await issueMutation.mutateAsync(paymentId)
      if (useAuthStore.getState().session?.id !== staffId || paymentId !== activePaymentId) return issuedResult
      await queryClient.cancelQueries({ queryKey: [...RENTAL_PAYMENT_QUERY_KEY, staffId, paymentId] })
      queryClient.setQueryData([...RENTAL_PAYMENT_QUERY_KEY, staffId, paymentId], (current) => current ? { ...current, status: 'ISSUED', issuedResult } : current)
      queryClient.invalidateQueries({ queryKey: RENTALS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: RENTAL_DETAILS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: INVENTORY_HISTORY_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: RENTAL_PAYMENTS_QUERY_KEY })
      frozenIssue.current = null
      return issuedResult
    } catch (error) {
      if (!isRentalRequestOutcomeUncertain(error)) frozenIssue.current = null
      throw error
    }
  }, [activePaymentId, issueMutation, queryClient, staffId])

  const cancel = useCallback(async () => {
    if (!activePaymentId) return null
    const paymentId = frozenCancel.current || activePaymentId
    frozenCancel.current = paymentId
    cancelMutation.reset()
    try {
      const result = await cancelMutation.mutateAsync(paymentId)
      await paymentQuery.refetch()
      queryClient.invalidateQueries({ queryKey: RENTAL_PAYMENTS_QUERY_KEY })
      frozenCancel.current = null
      return result
    } catch (error) {
      if (!isRentalRequestOutcomeUncertain(error)) frozenCancel.current = null
      throw error
    }
  }, [activePaymentId, cancelMutation, paymentQuery, queryClient])

  const refresh = useCallback(async () => {
    const result = await paymentQuery.refetch()
    const status = result.data?.status
    if (status === 'ISSUED') frozenIssue.current = null
    if (status === 'PAID' && frozenIssue.current === activePaymentId) issueMutation.reset()
    if (isRentalPaymentWaiting(status) && frozenCancel.current === activePaymentId) cancelMutation.reset()
    return result
  }, [activePaymentId, cancelMutation, issueMutation, paymentQuery])

  const payment = paymentQuery.data
  const secondsLeft = clock?.paymentId === activePaymentId
    ? getRentalPaymentSecondsLeft(clock, now)
    : 0
  const createOutcomeUncertain = Boolean(createMutation.error && isRentalRequestOutcomeUncertain(createMutation.error))
  const issueOutcomeUncertain = Boolean(issueMutation.error && isRentalRequestOutcomeUncertain(issueMutation.error))
  const cancelOutcomeUncertain = Boolean(cancelMutation.error && isRentalRequestOutcomeUncertain(cancelMutation.error))

  return {
    payment,
    open,
    secondsLeft,
    activePaymentId,
    hasUncertainOutcome: createOutcomeUncertain || restoredCreateUncertain || issueOutcomeUncertain || cancelOutcomeUncertain,
    restoredCreateUncertain,
    creating: createMutation.isPending,
    creationError: createMutation.error,
    issuing: issueMutation.isPending,
    issueError: issueMutation.error,
    cancelling: cancelMutation.isPending,
    cancelError: cancelMutation.error,
    loading: Boolean(activePaymentId && paymentQuery.isPending),
    error: paymentQuery.error,
    refreshing: paymentQuery.isFetching,
    refresh,
    startQr,
    retryCreate,
    issue,
    cancel,
    openPayment,
    close: () => setOpen(false),
    finish: () => {
      saveRentalPaymentIntent(staffId, null)
      setActivePaymentId(null)
      setOpen(false)
      setClock(null)
    },
  }
}
