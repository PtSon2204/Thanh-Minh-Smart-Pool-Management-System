import { useEffect, useState } from 'react'
import { signalRIdentity } from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'
import { useAlarmSound } from '../hooks/useAlarmSound'
import { useGlobalSafetyAlerts } from '../hooks/useGlobalSafetyAlerts'
import { useSafetyTts } from '../hooks/useSafetyTts'
import GlobalAlertPopup from './GlobalAlertPopup'
import LiveAlertDetailsModal from './LiveAlertDetailsModal'
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
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [selection, setSelection] = useState(null)
  const selectedAlert = selection?.identity === identity ? selection.alert : null
  const enabled = role === 'ADMIN' || role === 'LIFEGUARD'
  const { alerts, top, dismiss, isCritical } = useGlobalSafetyAlerts(enabled)
  const alarmAlert = alerts.find(isCritical) || top
  const { startAlarm, stopAlarm } = useAlarmSound()
  // Mode camera-test mặc định im lặng; chỉ client test được chỉ định mới bật âm thanh.
  const audioAllowed = import.meta.env.MODE !== 'camera-test' || import.meta.env.VITE_SAFETY_TEST_AUDIO === 'true'
  useSafetyTts(enabled && audioAllowed)

  const critical = isCritical(alarmAlert)
  useEffect(() => {
    if (!enabled || !audioAllowed || !alarmAlert?.id) return undefined
    startAlarm(critical ? 'EMERGENCY' : 'WARNING')
    return () => stopAlarm()
  }, [enabled, audioAllowed, alarmAlert?.id, critical, startAlarm, stopAlarm])

  const viewLive = alert => { setSelection({ identity, alert }); dismiss(alert.id) }
  if (!enabled) return null
  return <>
    {top && <div className="safety-global-popup" role="alertdialog" aria-label="Cảnh báo an toàn">
    <div className="safety-popup-stack" style={{ width: 'min(560px, 100%)' }}>
      {alerts.map(alert => <GlobalAlertPopup key={alert.id} alert={alert} isCritical={isCritical} onDismiss={dismiss} onViewLive={viewLive} />)}
    </div>
    </div>}
    {selectedAlert && <LiveAlertDetailsModal key={selectedAlert.id} alert={selectedAlert} onClose={() => setSelection(null)} />}
  </>
}
