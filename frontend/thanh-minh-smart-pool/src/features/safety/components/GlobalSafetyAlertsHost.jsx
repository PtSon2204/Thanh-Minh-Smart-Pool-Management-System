import { useCallback, useEffect, useRef, useState } from 'react'
import { AlertOutlined, StopOutlined } from '@ant-design/icons'
import { getSignalRConnection, signalRIdentity } from '../../../services/signalrClient'
import { useAuthStore } from '../../auth/store/authStore'
import { setGlobalAlarmDucking, useAlarmSound } from '../hooks/useAlarmSound'
import { useGlobalSafetyAlerts } from '../hooks/useGlobalSafetyAlerts'
import { useSafetyTts } from '../hooks/useSafetyTts'
import GlobalAlertPopup from './GlobalAlertPopup'
import LiveAlertDetailsModal from './LiveAlertDetailsModal'
import '../styles/safetyResponsive.css'

const CONFIRMED_OVERLAY_MS = 30_000

/**
 * Khung gắn popup + còi + overlay + giọng nói cảnh báo an toàn toàn cục.
 * - Khi AI phát hiện (PENDING): chỉ hiện popup để nhân viên kiểm tra, chưa hú còi.
 * - Khi cứu hộ bấm Xác nhận WARNING: TTS đọc vị trí, KHÔNG hú còi, KHÔNG overlay.
 * - Khi cứu hộ bấm Xác nhận CRITICAL/EMERGENCY: hú còi + overlay toàn màn hình + TTS
 *   đọc vị trí (tắt cảnh báo không chuyển trang, tự tắt sau 30s).
 */
export default function GlobalSafetyAlertsHost() {
  const role = useAuthStore(state => state.session?.role?.toUpperCase())
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const [selection, setSelection] = useState(null)
  const [confirmed, setConfirmed] = useState(null)
  const stopTimerRef = useRef(null)
  const seenIncidentsRef = useRef(new Set())
  const selectedAlert = selection && selection.identity === identity ? selection.alert : null
  const confirmedAlert = confirmed && confirmed.identity === identity ? confirmed.alert : null
  const enabled = role === 'ADMIN' || role === 'LIFEGUARD'
  const { alerts, top, dismiss, isCritical } = useGlobalSafetyAlerts(enabled)
  const { startAlarm, stopAlarm } = useAlarmSound()
  // Mode camera-test mặc định im lặng; chỉ client test được chỉ định mới bật âm thanh.
  const audioAllowed = import.meta.env.MODE !== 'camera-test' || import.meta.env.VITE_SAFETY_TEST_AUDIO === 'true'
  useSafetyTts(enabled && audioAllowed)

  const stopConfirmedAlert = useCallback(() => {
    if (stopTimerRef.current) {
      clearTimeout(stopTimerRef.current)
      stopTimerRef.current = null
    }
    stopAlarm()
    setGlobalAlarmDucking(false)
    try { window.speechSynthesis?.cancel() } catch { /* Không hỗ trợ. */ }
    setConfirmed(null)
  }, [stopAlarm])

  useEffect(() => {
    if (!enabled || !identity) return undefined
    const seen = seenIncidentsRef.current
    const onConfirmed = alert => {
      if (alert?.status !== 'ACKNOWLEDGED' || !alert?.incidentId) return
      const key = `${identity}:${alert.incidentId}`
      if (seen.has(key)) return
      seen.add(key)

      const critical = ['CRITICAL', 'EMERGENCY'].includes(String(alert.severity || '').toUpperCase())

      if (!critical) {
        // WARNING: TTS đọc vị trí tự động qua useSafetyTts — không hú còi, không overlay
        return
      }

      // CRITICAL/EMERGENCY: hú còi + overlay toàn màn hình, tự tắt sau 30s
      setConfirmed({ identity, alert })
      if (audioAllowed) startAlarm('EMERGENCY')
      if (stopTimerRef.current) clearTimeout(stopTimerRef.current)
      stopTimerRef.current = setTimeout(() => {
        stopTimerRef.current = null
        stopAlarm()
        setConfirmed(null)
      }, CONFIRMED_OVERLAY_MS)
    }
    const onLocalAck = event => onConfirmed(event.detail)
    const connection = getSignalRConnection()
    connection.on('incident_created', onConfirmed)
    window.addEventListener('safety:acknowledged', onLocalAck)
    return () => {
      connection.off('incident_created', onConfirmed)
      window.removeEventListener('safety:acknowledged', onLocalAck)
      if (stopTimerRef.current) {
        clearTimeout(stopTimerRef.current)
        stopTimerRef.current = null
      }
      stopAlarm()
    }
  }, [enabled, identity, audioAllowed, startAlarm, stopAlarm])

  const viewLive = alert => { setSelection({ identity, alert }); dismiss(alert.id) }
  if (!enabled) return null

  const confirmedIsCritical = confirmedAlert ? isCritical(confirmedAlert) : false

  return <>
    {top && <div className="safety-global-popup" role="alertdialog" aria-label="Cảnh báo an toàn">
      <div className="safety-popup-stack" style={{ width: 'min(560px, 100%)' }}>
        {alerts.map(alert => <GlobalAlertPopup key={alert.id} alert={alert} isCritical={isCritical} onDismiss={dismiss} onViewLive={viewLive} />)}
      </div>
    </div>}
    {confirmedAlert && (
      <div
        className={`safety-confirmed-overlay ${confirmedIsCritical ? 'level-emergency' : 'level-warning'}`}
        role="alertdialog"
        aria-label="Cảnh báo cứu hộ đã xác nhận"
      >
        <div className="safety-confirmed-inner">
          <div className="safety-confirmed-icon"><AlertOutlined /></div>
          <div className="safety-confirmed-level">
            {confirmedIsCritical ? 'KHẨN CẤP' : 'CẢNH BÁO'}
          </div>
          <div className="safety-confirmed-zone">
            {confirmedAlert.location || confirmedAlert.cameraName || 'Khu vực bể bơi'}
          </div>
          {confirmedAlert.cameraName && (
            <div className="safety-confirmed-meta">Camera: {confirmedAlert.cameraName}</div>
          )}
          {confirmedAlert.imageUrl && (
            <img
              className="safety-confirmed-img"
              src={confirmedAlert.imageUrl}
              alt="Ảnh chụp sự cố"
            />
          )}
          <button type="button" className="safety-confirmed-stop-btn" onClick={stopConfirmedAlert}>
            <StopOutlined /> Tắt cảnh báo
          </button>
        </div>
      </div>
    )}
    {selectedAlert && <LiveAlertDetailsModal key={selectedAlert.id} alert={selectedAlert} onClose={() => setSelection(null)} />}
  </>
}
