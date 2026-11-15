import { useEffect, useRef } from 'react'
import { useAuthStore } from '../../auth/store/authStore'
import { getSignalRConnection, signalRIdentity, startSignalRConnection } from '../../../services/signalrClient'

// Cảnh báo thủ công dùng event riêng nhưng chia sẻ kết nối và token phiên đăng nhập.
export function useSafetyAlertSignalR({ onAlert, enabled = true }) {
  const callback = useRef(onAlert)
  const identity = useAuthStore(state => signalRIdentity(state.session))
  useEffect(() => { callback.current = onAlert }, [onAlert])
  useEffect(() => {
    if (!enabled || !identity) return undefined
    const connection = getSignalRConnection()
    const handle = payload => callback.current?.(payload)
    connection.on('safety_alert', handle)
    void startSignalRConnection()
    return () => { connection.off('safety_alert', handle) }
  }, [enabled, identity])
}
