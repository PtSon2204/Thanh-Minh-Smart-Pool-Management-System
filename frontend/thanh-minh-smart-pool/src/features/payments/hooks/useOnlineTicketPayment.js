import { useMemo, useRef, useState, useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuthStore } from '../../auth/store/authStore'
import ticketService from '../../tickets/services/ticketService'

const storageKey = (userId) => `smartpool-online-payment:${userId}`
const queryKey = (userId, orderId) => ['online-ticket-payment', userId, orderId]
const isWaiting = (status) => status === 'PENDING' || status === 'PARTIAL'

function readOrderId(userId) {
  if (!userId) return null
  try { return sessionStorage.getItem(storageKey(userId)) || null } catch { return null }
}

function saveOrderId(userId, orderId) {
  try {
    if (orderId) sessionStorage.setItem(storageKey(userId), orderId)
    else sessionStorage.removeItem(storageKey(userId))
  } catch { /* The server deadline still applies when browser storage is unavailable. */ }
}

export default function useOnlineTicketPayment() {
  const userId = useAuthStore((state) => state.session?.id)
  const queryClient = useQueryClient()
  const restoredId = useMemo(() => readOrderId(userId), [userId])
  const [session, setSession] = useState(null)
  const [modal, setModal] = useState(null)
  const [now, setNow] = useState(Date.now)
  const clock = useRef(null)
  const submitting = useRef(false)
  const orderId = session && session.userId === userId ? session.orderId : restoredId
  const open = Boolean(userId && orderId && (modal && modal.userId === userId ? modal.open : restoredId))

  const synchronizeClock = (data) => {
    const serverTime = Date.parse(data.serverTimeUtc)
    if (Number.isFinite(serverTime)) {
      clock.current = { userId, orderId: data.orderId, serverTime, receivedAt: performance.now() }
      setNow(serverTime)
    }
  }

  const statusQuery = useQuery({
    queryKey: queryKey(userId, orderId),
    enabled: Boolean(userId && orderId),
    queryFn: async ({ signal }) => {
      const data = await ticketService.checkMyOrderStatus(orderId, signal)
      if (useAuthStore.getState().session?.id === userId && !signal.aborted) synchronizeClock(data)
      return data
    },
    refetchInterval: (query) => isWaiting(query.state.data?.status) ? 3000 : false,
    refetchOnWindowFocus: true,
    retry: (attempt, error) => error.response?.status !== 404 && error.response?.status !== 403 && attempt < 2,
  })

  const checkout = useMutation({
    mutationFn: ticketService.checkoutOnline,
    retry: false,
  })

  useEffect(() => {
    if (!orderId) return
    const timer = setInterval(() => {
      const anchor = clock.current
      if (anchor && anchor.userId === userId && anchor.orderId === orderId) {
        setNow(anchor.serverTime + performance.now() - anchor.receivedAt)
      }
    }, 250)
    return () => clearInterval(timer)
  }, [userId, orderId])

  const start = async (payload) => {
    if (!userId) throw new Error('Vui lòng đăng nhập để thanh toán.')
    if (submitting.current) return
    if (orderId) {
      setModal({ userId, open: true })
      await statusQuery.refetch()
      return
    }
    submitting.current = true
    try {
      const data = await checkout.mutateAsync(payload)
      if (useAuthStore.getState().session?.id !== userId) return
      saveOrderId(userId, data.orderId)
      synchronizeClock(data)
      queryClient.setQueryData(queryKey(userId, data.orderId), data)
      setSession({ userId, orderId: data.orderId })
      setModal({ userId, open: true })
      return data
    } finally {
      submitting.current = false
    }
  }

  const close = () => setModal({ userId, open: false })
  const finish = () => {
    saveOrderId(userId, null)
    setSession({ userId, orderId: null })
    setModal({ userId, open: false })
    clock.current = null
  }

  const order = statusQuery.data
  const secondsLeft = order?.expiresAtUtc
    ? Math.max(0, Math.ceil((Date.parse(order.expiresAtUtc) - now) / 1000))
    : 0

  return {
    order,
    open,
    start,
    close,
    finish,
    secondsLeft,
    hasOrder: Boolean(orderId),
    checkingOut: checkout.isPending,
    loading: Boolean(orderId && statusQuery.isPending),
    error: statusQuery.error,
    refresh: statusQuery.refetch,
    refreshing: statusQuery.isFetching,
  }
}
