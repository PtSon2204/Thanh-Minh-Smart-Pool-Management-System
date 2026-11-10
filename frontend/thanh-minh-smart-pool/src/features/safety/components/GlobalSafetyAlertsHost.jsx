import { useEffect } from 'react'
import { useAuthStore } from '../../auth/store/authStore'
import { useAlarmSound } from '../hooks/useAlarmSound'
import { useGlobalSafetyAlerts } from '../hooks/useGlobalSafetyAlerts'
import { useSafetyTts } from '../hooks/useSafetyTts'
import GlobalAlertPopup from './GlobalAlertPopup'

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
  const { top, dismiss, isCritical } = useGlobalSafetyAlerts(enabled)
  const { startAlarm, stopAlarm } = useAlarmSound()
  useSafetyTts(enabled, top, isCritical)

  const critical = isCritical(top)
  useEffect(() => {
    if (!enabled || !top?.id) return undefined
    startAlarm(critical ? 'EMERGENCY' : 'WARNING')
    return () => stopAlarm()
  }, [enabled, top?.id, critical, startAlarm, stopAlarm])

  if (!enabled || !top) return null
  return <div style={{ position: 'fixed', insetInline: 0, top: 64, zIndex: 1050, display: 'flex', justifyContent: 'center', padding: '0 16px' }} role="alertdialog" aria-label="Cảnh báo an toàn">
    <div style={{ width: 'min(560px, 100%)' }}>
      <GlobalAlertPopup alert={top} isCritical={isCritical} onDismiss={dismiss} />
    </div>
  </div>
}
