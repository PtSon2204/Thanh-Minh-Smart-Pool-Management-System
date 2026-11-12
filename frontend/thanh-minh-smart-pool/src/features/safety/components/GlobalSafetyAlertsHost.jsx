import { useEffect } from 'react'
import { useAuthStore } from '../../auth/store/authStore'
import { useAlarmSound } from '../hooks/useAlarmSound'
import { useGlobalSafetyAlerts } from '../hooks/useGlobalSafetyAlerts'
import { useSafetyTts } from '../hooks/useSafetyTts'
import GlobalAlertPopup from './GlobalAlertPopup'
import '../styles/safetyResponsive.css'

/**
 * Khung gắn popup + còi + giọng nói cảnh báo an toàn toàn cục. Mount trong
 * MainLayout (trang công khai) và AdminLayout (trang vận hành) nên hiện trên
 * mọi trang. Chỉ hoạt động với người dùng có quyền vận hành an toàn
 * (ADMIN/LIFEGUARD) — các vai khác không nhận sự kiện cảnh báo AI, nên host này tự tắt.
 * Còi dùng singleton module; TTS (speechSynthesis) đọc ngắn bằng tiếng Việt.
 */
export default function GlobalSafetyAlertsHost() {
  const role = useAuthStore(state => state.session?.role?.toUpperCase())
  const enabled = role === 'ADMIN' || role === 'LIFEGUARD'
  const { alerts, top, dismiss, isCritical } = useGlobalSafetyAlerts(enabled)
  const alarmAlert = alerts.find(isCritical) || top
  const { startAlarm, stopAlarm } = useAlarmSound()
  useSafetyTts(enabled, alarmAlert, isCritical)

  const critical = isCritical(alarmAlert)
  useEffect(() => {
    if (!enabled || !alarmAlert?.id) return undefined
    startAlarm(critical ? 'EMERGENCY' : 'WARNING')
    return () => stopAlarm()
  }, [enabled, alarmAlert?.id, critical, startAlarm, stopAlarm])

  if (!enabled || !top) return null
  return <div className="safety-global-popup" role="alertdialog" aria-label="Cảnh báo an toàn">
    <div className="safety-popup-stack" style={{ width: 'min(560px, 100%)' }}>
      {alerts.map(alert => <GlobalAlertPopup key={alert.id} alert={alert} isCritical={isCritical} onDismiss={dismiss} />)}
    </div>
  </div>
}
