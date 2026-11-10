import { useEffect, useRef, useState } from 'react'
import { monitoringService } from '../services/monitoringService'

/**
 * Kéo decisions (box cảnh báo) của frame gần nhất theo vị trí phát hiện tại, dùng để
 * overlay bounding box lên video GÓC khi AI đang phân tích song song — khớp 1:1 với
 * video kết quả (vẽ khung sẵn) khi hoàn tất. Backend (RAM worker) trả decisions của
 * frame đã phân tích <= vị trí playhead, nên box "theo kịp" video mà không chờ hết video.
 * Poll ~500ms (nhanh hơn interval cảnh báo 3s) để box mượt khi video chạy.
 */
export function useVideoFrameAnalysis(jobId, position, enabled) {
  const [data, setData] = useState(null)
  const timer = useRef(null)

  useEffect(() => {
    if (!enabled) return undefined
    let cancelled = false
    const tick = async () => {
      try {
        const result = await monitoringService.frameAnalysis(jobId, position)
        if (!cancelled) setData(result)
      } catch {
        // 403 (không quyền job) hoặc backend chưa có endpoint: giữ khung trống, tick tiếp.
      }
      if (!cancelled) timer.current = setTimeout(tick, 500)
    }
    tick()
    return () => { cancelled = true; if (timer.current) clearTimeout(timer.current) }
  }, [jobId, position, enabled])

  // risks: box cảnh báo của frame gần nhất (khớp đúng các khung đã vẽ trong video kết quả).
  // ready: backend đã trả dữ liệu (dù rỗng = frame này không có cảnh báo — khác lỗi mạng).
  const risks = data?.risks || []
  return { risks, ready: data != null }
}
