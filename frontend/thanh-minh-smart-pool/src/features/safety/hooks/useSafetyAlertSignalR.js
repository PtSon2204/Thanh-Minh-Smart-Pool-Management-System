import { useEffect, useRef, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'

const SIGNALR_HUB_URL = `${import.meta.env.VITE_API_BASE_URL || ''}/hubs/notifications`

/**
 * useSafetyAlertSignalR
 * Kết nối đến SignalR hub và lắng nghe event "safety_alert".
 * Khi nhận được → gọi callback onAlert(payload).
 *
 * @param {function} onAlert - callback nhận payload cảnh báo
 * @param {boolean} enabled  - chỉ kết nối khi true
 */
export function useSafetyAlertSignalR({ onAlert, enabled = true }) {
  const connectionRef = useRef(null)
  const onAlertRef = useRef(onAlert)

  // Giữ ref luôn mới nhất để tránh stale closure
  useEffect(() => {
    onAlertRef.current = onAlert
  }, [onAlert])

  const getToken = useCallback(() => {
    try {
      // Lấy token từ localStorage (khớp với authStore)
      const raw = localStorage.getItem('auth-storage') || localStorage.getItem('auth_token') || ''
      if (!raw) return ''
      const parsed = JSON.parse(raw)
      return parsed?.state?.session?.accessToken || parsed?.accessToken || ''
    } catch {
      return ''
    }
  }, [])

  useEffect(() => {
    if (!enabled) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_HUB_URL, {
        accessTokenFactory: getToken,
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    // Lắng nghe event từ backend: INotificationService.SendToAllAsync("safety_alert", payload)
    connection.on('safety_alert', (payload) => {
      onAlertRef.current?.(payload)
    })

    connection
      .start()
      .catch((err) => console.warn('[SafetyAlert] SignalR connect error:', err))

    return () => {
      connection.off('safety_alert')
      connection.stop()
    }
  }, [enabled, getToken])
}
