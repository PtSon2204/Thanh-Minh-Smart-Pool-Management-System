import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getSignalRConnection, onSignalRStateChange, startSignalRConnection, signalRIdentity,
} from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'
import { monitoringService } from '../services/monitoringService'
import { reconcilePendingAlerts, sortAlerts } from '../utils/alertState'
import { ALERT_POPUP_LIFETIME_MS } from '../utils/popupLifetime'

// Cảnh báo cứu hộ toàn cục chỉ nhận nguồn LIVE. Mỗi danh tính có phiên popup riêng.
const AI_EVENTS = ['alert_created', 'alert_escalated', 'alert_updated', 'incident_created']
export function useGlobalSafetyAlerts(enabled) {
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [snapshot, setSnapshot] = useState({ identity: null, alerts: [] })
  const dismissal = useRef(null)
  useEffect(() => {
    if (!enabled) return undefined
    let disposed = false
    let syncing = false
    let revision = 0
    let active = []
    const dismissed = new Set()
    const expiryTimers = new Map()
    const abort = new AbortController()
    const publish = () => { if (!disposed) setSnapshot({ identity, alerts: sortAlerts(active) }) }
    const dismiss = id => {
      clearTimeout(expiryTimers.get(id))
      expiryTimers.delete(id)
      dismissed.add(id)
      active = active.filter(a => a.id !== id)
      revision++
      publish()
    }
    const scheduleExpiry = () => {
      for (const alert of active) {
        if (!expiryTimers.has(alert.id)) expiryTimers.set(alert.id, setTimeout(() => dismiss(alert.id), ALERT_POPUP_LIFETIME_MS))
      }
      for (const [id, timer] of expiryTimers) {
        if (!active.some(alert => alert.id === id)) { clearTimeout(timer); expiryTimers.delete(id) }
      }
    }
    dismissal.current = { identity, dismiss }
    const resync = async () => {
      if (syncing || disposed) return
      syncing = true
      const before = revision
      try {
        const pending = []
        let page = 1
        let total
        do {
          const data = await monitoringService.alerts({ sourceType: 'LIVE', status: 'PENDING', page, pageSize: 100 }, abort.signal)
          pending.push(...(data.items || []))
          total = data.totalCount || 0
          page++
          if (!data.items?.length) break
        } while (pending.length < total && !disposed)
        // Event nhận sau khi bắt đầu HTTP mới hơn snapshot đang tải.
        if (!disposed && before === revision) {
          active = reconcilePendingAlerts(active, pending, dismissed, Date.now())
          scheduleExpiry()
          publish()
        }
      } catch {
        // Mất mạng: giữ cảnh báo đang hiện, thử lại ở lần đồng bộ sau.
      } finally { syncing = false }
    }
    const connection = getSignalRConnection()
    const handle = alert => {
      if (!alert?.id || alert.sourceType !== 'LIVE') return
      revision++
      active = active.filter(a => a.id !== alert.id)
      if (alert.status?.toUpperCase() === 'PENDING' && !dismissed.has(alert.id)) active.push(alert)
      scheduleExpiry()
      publish()
    }
    AI_EVENTS.forEach(event => connection.on(event, handle))
    const offState = onSignalRStateChange(next => { if (next === 'connected') void resync() })
    void startSignalRConnection()
    void resync()
    const timer = setInterval(() => void resync(), 15000)
    return () => {
      disposed = true
      abort.abort()
      clearInterval(timer)
      expiryTimers.forEach(timer => clearTimeout(timer))
      AI_EVENTS.forEach(event => connection.off(event, handle))
      offState()
    }
  }, [enabled, identity])
  const dismiss = useCallback(id => {
    if (enabled && dismissal.current?.identity === identity) dismissal.current.dismiss(id)
  }, [enabled, identity])
  const alerts = enabled && snapshot.identity === identity ? snapshot.alerts : []
  return { alerts, top: alerts[0] || null, dismiss,
    isCritical: a => a?.severity === 'CRITICAL' || a?.severity === 'EMERGENCY' }
}
