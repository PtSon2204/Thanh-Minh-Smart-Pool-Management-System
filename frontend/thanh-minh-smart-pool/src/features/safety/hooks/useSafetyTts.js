import { useEffect } from 'react'
import { useAuthStore } from '../../auth/store/authStore'
import { getSignalRConnection, signalRIdentity } from '../../../services/signalrClient'
import { criticalAnnouncement } from '../utils/criticalAnnouncement'

/**
 * Giọng đọc sự cố LIVE sau xác nhận thành công (Web Speech API).
 * Chỉ hoạt động khi trình duyệt hỗ trợ `speechSynthesis`; không chặn luồng chính.
 * Dedupe theo IncidentId và danh tính trong tab, kể cả khi event/HTTP đến đồng thời.
 */
export function useSafetyTts(enabled) {
  const identity = useAuthStore(state => signalRIdentity(state.session))
  useEffect(() => {
    if (!enabled || typeof window === 'undefined' || !('speechSynthesis' in window)) return undefined
    const spoken = new Set()
    const announce = alert => {
      const announcement = criticalAnnouncement(alert)
      if (!announcement || spoken.has(announcement.id)) return
      const key = `safety-tts:${identity}:${announcement.id}`
      try { if (window.sessionStorage.getItem(key)) return } catch { /* Bộ nhớ có thể bị chặn. */ }
      spoken.add(announcement.id)
      try { window.sessionStorage.setItem(key, '1') } catch { /* Dedupe trong mount vẫn hoạt động. */ }
      try {
        const utterance = new SpeechSynthesisUtterance(announcement.text)
        utterance.lang = 'vi-VN'
        utterance.rate = 1
        const vi = window.speechSynthesis.getVoices?.().find(voice => voice.lang?.toLowerCase().startsWith('vi'))
        if (vi) utterance.voice = vi
        window.speechSynthesis.speak(utterance)
      } catch { /* Không chặn xác nhận nếu trình duyệt không phát được. */ }
    }
    const local = event => announce(event.detail)
    const connection = getSignalRConnection()
    connection.on('incident_created', announce)
    window.addEventListener('safety:acknowledged', local)
    return () => {
      connection.off('incident_created', announce)
      window.removeEventListener('safety:acknowledged', local)
      try { window.speechSynthesis.cancel() } catch { /* Không hỗ trợ. */ }
    }
  }, [enabled, identity])
}
