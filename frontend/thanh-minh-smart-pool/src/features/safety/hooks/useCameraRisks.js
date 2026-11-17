import { useCallback, useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'
import { subscribeCameraAnalysis } from '../services/cameraAnalysisStream'
import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS, MONITORING_STALE_MS, mergeCameraAnalysis, metadataRejectionReason } from '../utils/cameraAnalysisState'
import { useAuthStore } from '../../auth/store/authStore'
import { signalRIdentity } from '../../../services/signalrClient'
import { CameraRiskFrameBuffer } from '../utils/cameraRiskFrameBuffer'
import { cameraDiagnostics } from '../utils/cameraDiagnostics'

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
      const incoming = await monitoringService.analysis(cameraId, signal)
      const current = client.getQueryData(['safety', 'camera-analysis', cameraId])
      const reason = metadataRejectionReason(current, incoming)
      const merged = mergeCameraAnalysis(current, incoming, Date.now())
      frames.add(merged, merged.receivedAt)
      cameraDiagnostics.record(cameraId, { source: 'http-poll', at: Date.now(),
        rtpTimestamp: incoming.rtpTimestamp ?? null, frameId: incoming.frameId ?? null,
        trackingSession: incoming.trackingSession ?? null, riskCount: incoming.activeRiskTrackIds?.length ?? 0,
        rejected: Boolean(reason), reason })
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
        const reason = metadataRejectionReason(current, payload)
        const merged = mergeCameraAnalysis(current, payload, Date.now())
        frames.add(merged, merged.receivedAt)
        cameraDiagnostics.record(cameraId, { source: 'signalr', at: Date.now(),
          rtpTimestamp: payload.rtpTimestamp ?? null, frameId: payload.frameId ?? null,
          trackingSession: payload.trackingSession ?? null, riskCount: payload.activeRiskTrackIds?.length ?? 0,
          rejected: Boolean(reason), reason })
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
  // Observation-only companion: same risks as sampleRisks, plus the exact empty
  // reason so the repro procedure can classify a disappearance. Bounded.
  const sampleRisksWithDiagnostics = useCallback(timestamp => {
    const result = frames.sampleWithDiagnostics(timestamp)
    if (cameraDiagnostics.shouldSampleFrame(cameraId, Date.now())) {
      cameraDiagnostics.record(cameraId, { source: 'render', at: Date.now(),
        rtpTimestamp: timestamp ?? null, riskCount: result.risks.length, reason: result.reason,
        metadataAgeMs: result.metadataAgeMs, metadataRtpTimestamp: result.metadataRtpTimestamp })
    }
    return result.risks
  }, [frames, cameraId])
  const getPlayoutDelay = useCallback(() => frames.playoutDelayMs(), [frames])
  return { ai, fresh: monitored, risks: fresh ? (ai.data?.displayRisks ?? ai.data?.risks ?? []) : [],
    sampleRisks, sampleRisksWithDiagnostics, getPlayoutDelay }
}
