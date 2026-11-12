import { useEffect } from 'react'

/**
 * Cảnh báo an toàn bằng giọng nói (Web Speech API), chạy song song còi.
 * Chỉ hoạt động khi trình duyệt hỗ trợ `speechSynthesis`; không chặn luồng chính.
 * Key theo `alert.id` nên resync giữ cùng cảnh báo không đọc lại; đổi cảnh báo
 * mới hoặc hết cảnh báo thì dừng đọc dở và đọc cảnh báo mới.
 */
export function useSafetyTts(enabled, alert, isCritical) {
  const critical = alert ? isCritical(alert) : false
  useEffect(() => {
    if (!enabled || !alert || typeof window === 'undefined' || !('speechSynthesis' in window)) return undefined
    const where = alert.location ? `, khu vực ${alert.location}` : ''
    const line = critical
      ? `Cảnh báo khẩn cấp${where}. ${alert.cameraName || 'Camera'}. Cần cứu hộ ngay.`
      : `Cảnh báo an toàn${where}. ${alert.cameraName || 'Camera'}.`
    try {
      window.speechSynthesis.cancel()
      const utterance = new SpeechSynthesisUtterance(line)
      utterance.lang = 'vi-VN'
      utterance.rate = 1.0
      utterance.volume = 1.0
      utterance.pitch = 1.0
      // Ưu tiên giọng Việt nếu hệ thống có; không thì giọng mặc định.
      const vi = (window.speechSynthesis.getVoices?.() || []).find(v => v.lang?.toLowerCase().startsWith('vi'))
      if (vi) utterance.voice = vi
      window.speechSynthesis.speak(utterance)
    } catch {
      // Trình duyệt chặn/ không hỗ trợ — bỏ qua, còi vẫn chạy.
    }
    return () => { try { window.speechSynthesis.cancel() } catch { /* ignore */ } }
    // Chỉ đọc lại khi đổi cảnh báo (id) hoặc mức độ đổi, không đọc lại khi resync.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, alert?.id, critical])
}
