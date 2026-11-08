import { useCallback, useEffect, useRef, useState } from 'react'
import {
  getSignalRConnection, onSignalRStateChange, startSignalRConnection, signalRIdentity,
} from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'
import { monitoringService } from '../services/monitoringService'

const AI_EVENTS = ['alert_created', 'alert_escalated', 'alert_updated', 'incident_created']

function priority(a) {
  const critical = a.severity === 'CRITICAL' || a.severity === 'EMERGENCY'
  return (critical ? 0 : 1) * 100000 + (100000 - (Date.parse(a.createdAt) % 100000))
}

/**
 * Danh sách cảnh báo PENDING mà người dùng này có quyền thấy (LIVE + video UPLOAD của
 * chính họ), được cập nhật realtime qua SignalR và tự đồng bộ lại khi kết nối (lại).
 * Người dùng đóng popup của một cảnh báo sẽ không hiện lại cảnh báo đó trong phiên.
 */
export function useGlobalSafetyAlerts(enabled) {
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [active, setActive] = useState([])
  const dismissed = useRef(new Set())

  const drop = useCallback(id => setActive(list => list.filter(a => a.id !== id)), [])
  const upsert = useCallback(alert => {
    if (!alert?.id) return
    const isPending = (alert.status || '').toUpperCase() === 'PENDING'
    setActive(list => {
      const rest = list.filter(a => a.id !== alert.id)
      // Cảnh báo không còn PENDING (đã xác nhận/hủy/ảnh xong) thì xoá khỏi popup.
      return isPending && !dismissed.current.has(alert.id) ? [alert, ...rest] : rest
    })
  }, [])

  const resync = useCallback(async () => {
    try {
      const data = await monitoringService.alerts({ status: 'PENDING' })
      setActive((data?.items || []).filter(a => !dismissed.current.has(a.id)))
    } catch {
      // Trạng thái offline: giữ popup hiện có, sự kiện tiếp theo sẽ cập nhật.
    }
  }, [])

  const dismiss = useCallback(id => {
    dismissed.current.add(id)
    drop(id)
  }, [drop])

  useEffect(() => {
    if (!enabled) return
    const connection = getSignalRConnection()
    const handle = (payload) => { upsert(payload); if (payload?.status && (payload.status || '').toUpperCase() !== 'PENDING') drop(payload.id) }
    AI_EVENTS.forEach(evt => connection.on(evt, handle))
    const offState = onSignalRStateChange(next => { if (next === 'connected') { void resync(); void startSignalRConnection() } })
    void startSignalRConnection()
    void resync()
    return () => { AI_EVENTS.forEach(evt => connection.off(evt, handle)); offState() }
  }, [enabled, identity, upsert, drop, resync])

  const sorted = [...active].sort((a, b) => priority(a) - priority(b))
  return { alerts: sorted, top: sorted[0] || null, dismiss, isCritical: (a) => a?.severity === 'CRITICAL' || a?.severity === 'EMERGENCY' }
}
