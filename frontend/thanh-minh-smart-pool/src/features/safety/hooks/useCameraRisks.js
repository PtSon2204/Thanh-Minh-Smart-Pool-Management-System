import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'
import { subscribeCameraAnalysis } from '../services/cameraAnalysisStream'
import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS, mergeCameraAnalysis } from '../utils/cameraAnalysisState'
import { useAuthStore } from '../../auth/store/authStore'
import { signalRIdentity } from '../../../services/signalrClient'

/**
 * Trả về cảnh báo (risks) mới nhất của một camera để vẽ Bounding Box đè lên video.
 * `risks` rỗng khi dữ liệu AI quá 1 giây hoặc chưa cấu hình, tránh vẽ
 * khung hình cũ lên video trực tiếp (spec: "Không hiển thị metadata cũ").
 */
export function useCameraRisks(cameraId, enabled) {
  const [now, setNow] = useState(Date.now)
  const client = useQueryClient()
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const ai = useQuery({
    queryKey: ['safety', 'camera-analysis', cameraId],
    queryFn: async ({ signal }) => {
      const started = Date.now()
      const incoming = await monitoringService.analysis(cameraId, signal)
      const current = client.getQueryData(['safety', 'camera-analysis', cameraId])
      // A push received during this request is newer than an old offline HTTP response.
      if (current?.receivedAt > started && !incoming.processedAt) return current
      return mergeCameraAnalysis(current, incoming, Date.now())
    },
    enabled: enabled && !!cameraId, refetchInterval: enabled ? 1000 : false, retry: false,
  })
  useEffect(() => {
    if (!enabled || !cameraId || !identity) return undefined
    return subscribeCameraAnalysis(cameraId, payload => {
      client.setQueryData(['safety', 'camera-analysis', cameraId], current => mergeCameraAnalysis(current, payload, Date.now()))
    })
  }, [cameraId, enabled, identity, client])
  useEffect(() => {
    if (!enabled) return
    const timer = setInterval(() => setNow(Date.now()), 100)
    return () => clearInterval(timer)
  }, [enabled])
  const fresh = !ai.isError && ai.data?.state === 'Connected'
    && cameraAnalysisAge(ai.data, Math.max(now, ai.data?.receivedAt || now)) < MAX_OVERLAY_AGE_MS
  return { ai, fresh, risks: fresh ? (ai.data?.risks || []) : [] }
}
