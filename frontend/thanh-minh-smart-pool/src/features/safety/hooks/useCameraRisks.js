import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'

/**
 * Trả về cảnh báo (risks) mới nhất của một camera để vẽ Bounding Box đè lên video.
 * `risks` rỗng khi dữ liệu AI không còn mới (< 6s) hoặc chưa cấu hình, tránh vẽ
 * khung hình cũ lên video trực tiếp (spec: "Không hiển thị metadata cũ").
 */
export function useCameraRisks(cameraId, enabled) {
  const [now, setNow] = useState(Date.now)
  const ai = useQuery({
    queryKey: ['safety', 'camera-analysis', cameraId],
    queryFn: async ({ signal }) => ({ ...await monitoringService.analysis(cameraId, signal), receivedAt: Date.now() }),
    enabled: enabled && !!cameraId, refetchInterval: enabled ? 2000 : false, retry: false,
  })
  useEffect(() => {
    if (!enabled) return
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [enabled])
  const fresh = !ai.isError && ai.data?.state === 'Connected'
    && now - ai.data.receivedAt + Date.parse(ai.data.serverTime) - Date.parse(ai.data.observedAt) < 6000
  return { ai, fresh, risks: fresh ? (ai.data?.risks || []) : [] }
}
