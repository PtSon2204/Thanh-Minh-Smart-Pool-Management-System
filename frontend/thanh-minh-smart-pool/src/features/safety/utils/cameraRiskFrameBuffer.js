import { cameraAnalysisAge, MAX_OVERLAY_AGE_MS } from './cameraAnalysisState.js'
import { interpolateCameraBox } from './cameraBoxInterpolation.js'

const ticks = (a, b) => (a - b) | 0 // signed distance across the uint32 RTP wrap
const validBox = box => Array.isArray(box) && box.length === 4 && box.every(Number.isFinite) && box[2] > box[0] && box[3] > box[1]
const MAX_HOLD_TICKS = 81000 // 900 ms: bridge the ~300 ms playout delay and a delayed push.
const MAX_PREDICTION_TICKS = 18000 // Predict motion briefly, then hold until the next observation.
const MAX_UNOBSERVED_TICKS = 45000 // Bridge a brief detector confidence dip for at most 500 ms.

function predictBox(risk, past, previous, behind) {
  const detection = previous?.detections?.find(d => d.trackId === risk.trackId)
    || previous?.risks?.find(d => d.trackId === risk.trackId && d.episode === risk.episode)
  const elapsed = previous && ticks(past.rtpTimestamp, previous.rtpTimestamp)
  if (!validBox(detection?.box) || elapsed <= 0 || elapsed > MAX_HOLD_TICKS) return risk
  // Keep the last measured size; only extrapolate the centre to avoid pulsing boxes.
  const weight = Math.min(behind, MAX_PREDICTION_TICKS) / elapsed
  const box = risk.box
  const dx = Math.max(-box[0], Math.min(1 - box[2], ((box[0] + box[2] - detection.box[0] - detection.box[2]) / 2) * weight))
  const dy = Math.max(-box[1], Math.min(1 - box[3], ((box[1] + box[3] - detection.box[1] - detection.box[3]) / 2) * weight))
  return { ...risk, box: [box[0] + dx, box[1] + dy, box[2] + dx, box[3] + dy] }
}

// Video follows the source clock; only box coordinates are interpolated between
// real AI observations. IDs, severity and episodes always come from the past sample.
export class CameraRiskFrameBuffer {
  samples = []
  latest = null
  latched = new Map()
  playoutDelayMs() {
    const samples = this.samples.slice(-12)
    if (!samples.length) return 300
    const costs = samples.map(item => Date.parse(item.serverTime) - Date.parse(item.observedAt)).filter(value => Number.isFinite(value) && value >= 0)
    const gaps = samples.slice(1).map((item, index) => ticks(item.rtpTimestamp, samples[index].rtpTimestamp) / 90)
      .filter(value => value > 0 && value <= 1000)
    const percentile = values => values.sort((a, b) => a - b)[Math.floor((values.length - 1) * .9)] || 0
    // One observation interval of lookahead, plus analysis and push jitter.
    return Math.min(300, Math.max(160, percentile(costs) + percentile(gaps) + 75))
  }
  add(data, receivedAt = Date.now()) {
    if (this.latest && Date.parse(data?.processedAt) < Date.parse(this.latest.processedAt)) return
    if (data?.state !== 'Connected' || !Number.isInteger(data.rtpTimestamp)) {
      this.samples = []; this.latest = null; this.latched.clear(); return
    }
    if (this.latest && data.trackingSession !== this.latest.trackingSession) { this.samples = []; this.latched.clear() }
    const sample = { ...data, receivedAt }
    const active = new Set(data.activeRiskTrackIds || [])
    const display = (data.displayRisks || []).filter(risk => active.has(risk.trackId)
      && data.detections?.some(d => d.trackId === risk.trackId && validBox(d.box)))
    const measuredRisks = new Map([...display, ...(data.risks || [])].filter(risk => validBox(risk.box)).map(risk => [risk.trackId, risk]))
    for (const id of this.latched.keys()) if (!active.has(id) && !measuredRisks.has(id)) this.latched.delete(id)
    for (const [id, risk] of measuredRisks) this.latched.set(id, { risk, observed: data.rtpTimestamp })
    sample.displayRisks = []
    sample.heldGeometry = new Map()
    for (const [id, entry] of this.latched) {
      if (!measuredRisks.has(id)) {
        const detection = data.detections?.find(d => d.trackId === id && validBox(d.box))
        if (detection) {
          entry.risk = { ...entry.risk, box: detection.box }
          entry.observed = data.rtpTimestamp
        } else {
          sample.heldGeometry.set(id, entry.observed)
          if (ticks(data.rtpTimestamp, entry.observed) > MAX_UNOBSERVED_TICKS) continue
        }
      }
      sample.displayRisks.push(entry.risk)
    }
    this.latest = sample
    const index = this.samples.findIndex(old => old.frameId === sample.frameId)
    if (index >= 0) this.samples[index] = sample
    else this.samples.push(sample)
    this.samples.sort((a, b) => Date.parse(a.processedAt) - Date.parse(b.processedAt))
    if (this.samples.length > 32) this.samples.splice(0, this.samples.length - 32)
  }
  sampleWithDiagnostics(timestamp, now = Date.now()) {
    const age = this.latest ? cameraAnalysisAge(this.latest, now) : Infinity
    let reason = null
    if (!Number.isInteger(timestamp)) reason = 'invalid-video-rtp'
    else if (!this.latest) reason = 'no-latest-metadata'
    else if (age >= MAX_OVERLAY_AGE_MS) reason = 'metadata-stale'
    const risks = this.sample(timestamp, now)
    if (!reason && risks.length === 0) {
      let past = false
      let future = false
      let behind = Infinity
      let ahead = Infinity
      for (const item of this.samples) {
        const distance = ticks(timestamp, item.rtpTimestamp)
        if (distance >= 0 && distance < behind) { past = true; behind = distance }
        if (distance < 0 && -distance < ahead) { future = true; ahead = -distance }
      }
      if (!past) reason = 'no-past-sample'
      else if (behind > MAX_HOLD_TICKS && (!future || behind + ahead > 90000)) reason = 'outside-hold-window'
      else if (this.latest.displayRisks?.length) reason = 'held-risk-expired'
      else reason = 'no-visible-risks'
    }
    return { risks, reason: reason || 'risks-present', metadataAgeMs: age,
      metadataRtpTimestamp: this.latest?.rtpTimestamp ?? null }
  }

  sample(timestamp, now = Date.now()) {
    if (!Number.isInteger(timestamp) || !this.latest || cameraAnalysisAge(this.latest, now) >= MAX_OVERLAY_AGE_MS) return []
    let past, future, behind = Infinity, ahead = Infinity
    for (const item of this.samples) {
      const distance = ticks(timestamp, item.rtpTimestamp)
      if (distance >= 0 && distance < behind) { past = item; behind = distance }
      if (distance < 0 && -distance < ahead) { future = item; ahead = -distance }
    }
    if (!past || behind > MAX_HOLD_TICKS && (!future || behind + ahead > 90000)) return []
    let previous, previousDistance = Infinity
    for (const item of this.samples) {
      const distance = ticks(past.rtpTimestamp, item.rtpTimestamp)
      if (distance > 0 && distance < previousDistance) { previous = item; previousDistance = distance }
    }
    // Geometry is renewed only by real detections. Risk colour stays latched
    // independently, while missing people still expire instead of leaving ghosts.
    const held = past.heldGeometry
    const visible = past.displayRisks.filter(risk => {
      if (held.has(risk.trackId) && ticks(timestamp, held.get(risk.trackId)) > MAX_UNOBSERVED_TICKS) return false
      if (behind <= MAX_HOLD_TICKS) return true
      const next = future?.detections?.find(d => d.trackId === risk.trackId)
        || future?.risks?.find(d => d.trackId === risk.trackId && d.episode === risk.episode)
      return !held.has(risk.trackId) && validBox(next?.box)
    })
    return visible.map(risk => {
      const next = future?.detections?.find(d => d.trackId === risk.trackId)
        || future?.risks?.find(d => d.trackId === risk.trackId && d.episode === risk.episode)
      if (held.has(risk.trackId)) return risk
      if (!future) return predictBox(risk, past, previous, behind)
      if (!next || !validBox(next.box) || behind + ahead > 90000) return risk
      const weight = behind / (behind + ahead)
      const earlier = previous?.detections?.find(d => d.trackId === risk.trackId)
        || previous?.risks?.find(d => d.trackId === risk.trackId && d.episode === risk.episode)
      let following, followingDistance = Infinity
      for (const item of this.samples) {
        const distance = ticks(item.rtpTimestamp, future.rtpTimestamp)
        if (distance <= 0 || distance >= followingDistance) continue
        const detection = item.detections?.find(d => d.trackId === risk.trackId)
          || item.risks?.find(d => d.trackId === risk.trackId && d.episode === risk.episode)
        if (validBox(detection?.box)) { following = { box: detection.box, timestamp: item.rtpTimestamp }; followingDistance = distance }
      }
      return { ...risk, box: interpolateCameraBox({ box: risk.box, timestamp: past.rtpTimestamp },
        { box: next.box, timestamp: future.rtpTimestamp }, weight,
        !held.has(risk.trackId) && validBox(earlier?.box) ? { box: earlier.box, timestamp: previous.rtpTimestamp } : undefined,
        held.has(risk.trackId) ? undefined : following) }
    })
  }
}
