import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getSignalRConnection, onSignalRStateChange, startSignalRConnection, signalRIdentity,
} from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'
import { subscribeCameraAnalysis } from '../services/cameraAnalysisStream'
import { monitoringService } from '../services/monitoringService'
import { isRecentLiveAlert, mergeAlertUpdate, popupIdentity, reconcilePendingAlerts, sortAlerts, cameraRiskState } from '../utils/alertState'
import { ALERT_POPUP_LIFETIME_MS } from '../utils/popupLifetime'

// Cảnh báo cứu hộ toàn cục chỉ nhận nguồn LIVE. Mỗi danh tính có phiên popup riêng.
// Popup LIVE tồn tại cho tới khi rủi ro trên camera kết thúc: stream camera_analysis
// của camera đó còn track/box của cảnh báo thì timer được làm mới (giữ mở); khi
// stream báo track đã biến mất thì đóng ngay (nếu chưa có thao tác). Timer 10s chỉ
// còn là dự phòng khi stream im lặng (mất kết nối camera).
const AI_EVENTS = ['alert_created', 'alert_escalated', 'alert_updated', 'incident_created']
export function useGlobalSafetyAlerts(enabled) {
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [snapshot, setSnapshot] = useState({ identity: null, alerts: [] })
  const dismissal = useRef(null)
  useEffect(() => {
    if (!enabled || !identity) return undefined
    let disposed = false
    let syncing = false
    let revision = 0
    let active = []
    const dismissed = new Set()
    const expiryTimers = new Map()
    const cameraSubs = new Map()
    const abort = new AbortController()
    // Stream camera của từng camera có cảnh báo LIVE đang mở. Module
    // subscribeCameraAnalysis đã counting: vài người xem cùng camera vẫn chỉ có
    // một group subscribe phía server, và giải phóng khi popup cuối cùng đóng.
    const onCameraFrame = payload => {
      if (disposed || !payload) return
      for (const alert of active) {
        if (alert.sourceType !== 'LIVE') continue
        const version = popupIdentity(alert)
        if (dismissed.has(version)) continue
        const state = cameraRiskState(payload, alert)
        if (state === 'present') {
          // Rủi vẫn còn trên camera: làm mới timer dự phòng, popup giữ mở.
          const timer = expiryTimers.get(version)
          if (timer) { clearTimeout(timer); expiryTimers.set(version, setTimeout(() => dismiss(alert.id, version), ALERT_POPUP_LIFETIME_MS)) }
        } else if (state === 'cleared') {
          // Camera hết cảnh báo (track đó không còn rủi ro) và chưa thao tác gì
          // -> đóng popup ngay; "unknown" (stream chưa rõ) để timer dự phòng quyết.
          dismiss(alert.id, null)
        }
      }
    }
    const syncCameraSubscriptions = () => {
      const wanted = new Set(active.filter(alert => alert.sourceType === 'LIVE' && alert.cameraId)
        .map(alert => String(alert.cameraId).toLowerCase()))
      for (const id of [...cameraSubs.keys()]) if (!wanted.has(id)) { cameraSubs.get(id)(); cameraSubs.delete(id) }
      for (const id of wanted) if (!cameraSubs.has(id)) cameraSubs.set(id, subscribeCameraAnalysis(id, onCameraFrame))
    }
    const publish = () => { if (!disposed) { setSnapshot({ identity, alerts: sortAlerts(active) }); syncCameraSubscriptions() } }
    const dismiss = (id, expectedVersion) => {
      const alert = active.find(a => a.id === id)
      if (!alert || expectedVersion && popupIdentity(alert) !== expectedVersion) return
      const version = popupIdentity(alert)
      clearTimeout(expiryTimers.get(version))
      expiryTimers.delete(version)
      dismissed.add(version)
      active = active.filter(a => a.id !== id)
      revision++
      publish()
    }
    const scheduleExpiry = () => {
      for (const alert of active) {
        const version = popupIdentity(alert)
        if (!expiryTimers.has(version)) expiryTimers.set(version, setTimeout(() => dismiss(alert.id, version), ALERT_POPUP_LIFETIME_MS))
      }
      for (const [version, timer] of expiryTimers) {
        if (!active.some(alert => popupIdentity(alert) === version)) { clearTimeout(timer); expiryTimers.delete(version) }
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
        // A concurrent update/paginated snapshot can omit a visible row. Only
        // a confirmed terminal detail response may close it before its timer.
        if (!disposed && before === revision) {
          const ids = new Set(pending.map(alert => alert.id))
          const missing = active.filter(alert => !ids.has(alert.id))
          const details = await Promise.allSettled(missing.map(alert => monitoringService.alert(alert.id, abort.signal)))
          for (const result of details) if (result.status === 'fulfilled' && result.value?.id) pending.push(result.value)
        }
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
      if (disposed || !alert?.id || alert.sourceType !== 'LIVE') return
      const current = active.find(a => a.id === alert.id)
      if (mergeAlertUpdate(current, alert) !== alert) return
      if (alert.status?.toUpperCase() === 'PENDING' && dismissed.has(popupIdentity(alert))) return
      revision++
      active = active.filter(a => a.id !== alert.id)
      if (alert.status?.toUpperCase() !== 'PENDING') dismissed.add(popupIdentity(alert))
      if (alert.status?.toUpperCase() === 'PENDING' && !dismissed.has(popupIdentity(alert))
        && (current && popupIdentity(current) === popupIdentity(alert) || isRecentLiveAlert(alert, Date.now()))) active.push(alert)
      scheduleExpiry()
      publish()
    }
    AI_EVENTS.forEach(event => connection.on(event, handle))
    const refreshed = event => handle(event.detail)
    window.addEventListener('safety:alert-refreshed', refreshed)
    const offState = onSignalRStateChange(next => { if (next === 'connected') void resync() })
    void startSignalRConnection()
    void resync()
    const timer = setInterval(() => void resync(), 15000)
    return () => {
      disposed = true
      abort.abort()
      clearInterval(timer)
      cameraSubs.forEach(unsubscribe => unsubscribe())
      cameraSubs.clear()
      expiryTimers.forEach(timer => clearTimeout(timer))
      AI_EVENTS.forEach(event => connection.off(event, handle))
      window.removeEventListener('safety:alert-refreshed', refreshed)
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
