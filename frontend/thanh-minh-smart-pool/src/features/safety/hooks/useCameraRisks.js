import { useCallback, useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'
import { subscribeCameraAnalysis } from '../services/cameraAnalysisStream'
import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS, MONITORING_STALE_MS, mergeCameraAnalysis } from '../utils/cameraAnalysisState'
import { useAuthStore } from '../../auth/store/authStore'
import { signalRIdentity } from '../../../services/signalrClient'
import { CameraRiskFrameBuffer } from '../utils/cameraRiskFrameBuffer'

/**
 * Trả về cảnh báo (risks) mới nhất của một camera để vẽ Bounding Box đè lên video.
 * `risks` rỗng khi dữ liệu AI quá 1 giây hoặc chưa cấu hình, tránh vẽ
 * khung hình cũ lên video trực tiếp (spec: "Không hiển thị metadata cũ").
 */
export function useCameraRisks(cameraId, enabled) {
  const [now, setNow] = useState(Date.now)
  const client = useQueryClient()
  const identity = useAuthStore(state => signalRIdentity(state.session))
  const frames = useMemo(() => new CameraRiskFrameBuffer(cameraId, identity), [cameraId, identity])
  const ai = useQuery({
    queryKey: ['safety', 'camera-analysis', cameraId],
    queryFn: async ({ signal }) => {
      const started = Date.now()
      const incoming = await monitoringService.analysis(cameraId, signal)
      const current = client.getQueryData(['safety', 'camera-analysis', cameraId])
      // A push received during this request is newer than an old offline HTTP response.
      if (current?.receivedAt > started && !incoming.processedAt) return current
      const merged = mergeCameraAnalysis(current, incoming, Date.now())
      frames.add(merged, merged.receivedAt)
      return merged
    },
    enabled: enabled && !!cameraId, refetchInterval: enabled ? 1000 : false, retry: false,
  })
  // Each viewer owns a timeline. A shared query may fetch through the card's
  // queryFn, so the dialog must also seed its timeline from the shared result.
  useEffect(() => {
    if (enabled && ai.data) frames.add(ai.data, ai.data.receivedAt)
  }, [enabled, ai.data, frames])
  useEffect(() => {
    if (!enabled || !cameraId || !identity) return undefined
    return subscribeCameraAnalysis(cameraId, payload => {
      client.setQueryData(['safety', 'camera-analysis', cameraId], current => {
        const merged = mergeCameraAnalysis(current, payload, Date.now())
        frames.add(merged, merged.receivedAt)
        return merged
      })
    })
  }, [cameraId, enabled, identity, client, frames])
  useEffect(() => {
    if (!enabled) return
    const timer = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(timer)
  }, [enabled])
  // Nhãn "đang giám sát" (monitored) chỉ đổi khi không có dữ liệu phân tích trong
  // 10s, tránh nhấp nháy theo nhịp push từng frame. `risks` (dữ liệu để vẽ box)
  // vẫn dùng ngưỡng 1s để không hiển thị metadata cũ.
  const monitored = !ai.isError && ai.data?.state === 'Connected'
    && cameraAnalysisAge(ai.data, Math.max(now, ai.data?.receivedAt || now)) < MONITORING_STALE_MS
  const fresh = monitored && cameraAnalysisAge(ai.data, Math.max(now, ai.data?.receivedAt || now)) < MAX_OVERLAY_AGE_MS
  const sampleRisks = useCallback(timestamp => frames.sample(timestamp), [frames])
  const getPlayoutDelay = useCallback(() => frames.playoutDelayMs(), [frames])
  return { ai, fresh: monitored, risks: fresh ? (ai.data?.displayRisks ?? ai.data?.risks ?? []) : [], sampleRisks, getPlayoutDelay }
}
