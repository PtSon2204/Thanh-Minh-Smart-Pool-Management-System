import { useEffect } from 'react'
import { useAuthStore } from '../../auth/store/authStore'
import { getSignalRConnection, onSignalRStateChange, signalRIdentity, startSignalRConnection } from '../../../services/signalrClient'

// Cùng kết nối với cảnh báo AI; polling bù event bị lỡ và cập nhật snapshot đến muộn.
export function useIncidentEvents(refresh) {
  const identity = useAuthStore(state => signalRIdentity(state.session))
  useEffect(() => {
    const connection = getSignalRConnection()
    const events = ['safety_alert', 'incident_created', 'incident_updated', 'alert_updated']
    events.forEach(event => connection.on(event, refresh))
    const off = onSignalRStateChange(state => { if (state === 'connected') refresh() })
    void startSignalRConnection()
    const timer = setInterval(refresh, 15000)
    return () => { events.forEach(event => connection.off(event, refresh)); off(); clearInterval(timer) }
  }, [identity, refresh])
}
