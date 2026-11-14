import { useCallback, useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'
import { subscribeCameraAnalysis } from '../services/cameraAnalysisStream'
import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS, MONITORING_STALE_MS, mergeCameraAnalysis } from '../utils/cameraAnalysisState'
import { useAuthStore } from '../../auth/store/authStore'
import { signalRIdentity } from '../../../services/signalrClient'
import { CameraRiskFrameBuffer } from '../utils/cameraRiskFrameBuffer'

// Lần nhận quan sát tốt cuối (Connected, metadata mới) cho mỗi camera, key
// cameraId|identity. Sticky state ở module: nhãn "đang giám sát" giữ 10s sau
// quan sát tốt nên một push "CameraOffline" đơn lẻ giữa các frame không làm
// tag nhấp nháy, mà không đụng state React lúc render.
const lastGoodAtByCamera = new Map()

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
  const cameraKey = `${cameraId}:${identity || 'anonymous'}`
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
  // Nhãn "đang giám sát" (monitored) là state dính (sticky): chỉ xuống "chờ dữ liệu"
  // khi đã 10s kể từ quan sát tốt cuối (Connected, metadata mới). Một push/poll
  // "CameraOffline" đơn lẻ giữa các frame không làm tag nhấp nháy. `fresh`
  // (dữ liệu để vẽ box) vẫn yêu cầu dữ liệu hiện tại dưới 1s.
  const dataGood = !ai.isError && ai.data?.state === 'Connected'
    && cameraAnalysisAge(ai.data, Math.max(now, ai.data?.receivedAt || now)) < MAX_OVERLAY_AGE_MS
  if (dataGood) lastGoodAtByCamera.set(cameraKey, now)
  const lastGoodAt = lastGoodAtByCamera.get(cameraKey)
  const monitored = Number.isFinite(lastGoodAt) && now - lastGoodAt < MONITORING_STALE_MS
  const fresh = monitored && cameraAnalysisAge(ai.data, Math.max(now, ai.data?.receivedAt || now)) < MAX_OVERLAY_AGE_MS
  const sampleRisks = useCallback(timestamp => frames.sample(timestamp), [frames])
  const getPlayoutDelay = useCallback(() => frames.playoutDelayMs(), [frames])
  return { ai, fresh: monitored, risks: fresh ? (ai.data?.displayRisks ?? ai.data?.risks ?? []) : [], sampleRisks, getPlayoutDelay }
}
