import { useEffect, useState } from 'react'
import { monitoringService } from '../services/monitoringService'
import { currentVideoRisks } from '../utils/videoFrameState'

// Không giữ box của job/vị trí trước khi seek hoặc khi request mới thất bại.
export function useVideoFrameAnalysis(jobId, position, enabled) {
  const [sample, setSample] = useState(null)
  useEffect(() => {
    if (!enabled || !jobId) return undefined
    let cancelled = false
    let timer
    const abort = new AbortController()
    const tick = async () => {
      try {
        const result = await monitoringService.frameAnalysis(jobId, position, abort.signal)
        if (!cancelled) setSample({ jobId, position, result })
      } catch {
        if (!cancelled) setSample(null)
      }
      if (!cancelled) timer = setTimeout(tick, 500)
    }
    void tick()
    return () => { cancelled = true; abort.abort(); clearTimeout(timer) }
  }, [jobId, position, enabled])
  const current = enabled && sample?.jobId === jobId && sample?.position === position ? sample.result : null
  return { risks: currentVideoRisks(current, position), ready: current != null }
}
