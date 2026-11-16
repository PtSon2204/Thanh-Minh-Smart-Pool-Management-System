import fs from 'node:fs'
import { CameraRiskFrameBuffer } from '../../frontend/thanh-minh-smart-pool/src/features/safety/utils/cameraRiskFrameBuffer.js'
const trace = JSON.parse(fs.readFileSync(process.argv[2] || 'backend/.test-runtime/box-continuity-trace.json'))
const ids = JSON.parse(fs.readFileSync(process.argv[3] || 'backend/.test-runtime/fluid-server-diagnostics.json')).map(x => x.id)
const percentile = (values, p) => [...values].sort((a,b) => a-b)[Math.floor((values.length - 1) * p)]
for (const [i, id] of ids.entries()) {
  const pushes = trace.analysis.filter(x => x.cameraId === id).sort((a,b) => a.time-b.time)
  const frames = trace.overlays[`Video camera Performance Camera ${i+1}`] || []
  let prediction = 0, expected = 0, missing = 0, emptyRun = 0, longestEmptyRun = 0
  const headroom = [], gaps = [], riskGaps = []
  const measuredCentreErrors = [], measuredSizeErrors = []
  const continuity = new CameraRiskFrameBuffer(), previousBehaviour = new CameraRiskFrameBuffer()
  let pushIndex = 0, bridgedFrames = 0, drawnBridgedFrames = 0
  const origin = Date.parse('2026-11-13T00:00:00Z')
  for (let j=1;j<pushes.length;j++) {gaps.push(pushes[j].time-pushes[j-1].time); if(pushes[j-1].risks?.length && !pushes[j].risks?.length) riskGaps.push({time:pushes[j].time,detections:pushes[j].detections?.length})}
  for (const frame of frames) {
    // Compare displayed rectangles to raw AI measurements at matching capture
    // timestamps. This measures renderer lag, not model accuracy against a human label.
    if (frame.canvasWidth && frame.videoWidth && Number.isInteger(frame.boxRtpTimestamp)) {
      const observed = pushes.find(x => x.rtpTimestamp === frame.boxRtpTimestamp && x.time <= frame.time)
      const scale = Math.min(frame.canvasWidth / frame.videoWidth, frame.canvasHeight / frame.videoHeight)
      const width = frame.videoWidth * scale, height = frame.videoHeight * scale
      const ox = (frame.canvasWidth - width) / 2, oy = (frame.canvasHeight - height) / 2
      for (const risk of observed?.risks || []) {
        const [x1, y1, x2, y2] = risk.box
        const cx = ox + (x1 + x2) * width / 2, cy = oy + (y1 + y2) * height / 2
        const nearest = frame.boxes?.map(box => ({ box, distance: Math.hypot(box[0] + box[2] / 2 - cx, box[1] + box[3] / 2 - cy) }))
          .sort((a,b) => a.distance - b.distance)[0]
        if (nearest) {
          measuredCentreErrors.push(nearest.distance)
          measuredSizeErrors.push(Math.hypot(nearest.box[2] - (x2-x1) * width, nearest.box[3] - (y2-y1) * height))
        }
      }
    }
    while (pushIndex < pushes.length && pushes[pushIndex].time <= frame.time) {
      const push = pushes[pushIndex]
      const data = { ...push, processedAt: new Date(origin + pushIndex).toISOString(),
        serverTime: new Date(origin + push.time).toISOString(), observedAt: new Date(origin + push.time - push.ageMs).toISOString() }
      continuity.add(data, origin + push.time)
      previousBehaviour.add({ ...data, activeRiskTrackIds: undefined }, origin + push.time)
      pushIndex++
    }
    const visible = continuity.sample(frame.rtpTimestamp, origin + frame.time)
    const old = previousBehaviour.sample(frame.rtpTimestamp, origin + frame.time)
    if (visible.length > 0 && old.length === 0) {
      bridgedFrames++
      if (frame.boxes?.length) drawnBridgedFrames++
    }
    const available = pushes.filter(x => x.time <= frame.time)
    const last = available.at(-1)
    if (!last) continue
    const lead = ((last.rtpTimestamp - frame.rtpTimestamp)|0)/90
    headroom.push(lead)
    if (lead < 0) prediction++
    const past = available.filter(x=>((frame.rtpTimestamp-x.rtpTimestamp)|0)>=0).sort((a,b)=>((frame.rtpTimestamp-a.rtpTimestamp)|0)-((frame.rtpTimestamp-b.rtpTimestamp)|0))[0]
    if (past?.risks?.length) {
      expected++
      if (!frame.boxes?.length) { missing++; emptyRun++; longestEmptyRun=Math.max(longestEmptyRun,emptyRun) } else emptyRun=0
    } else emptyRun=0
  }
  console.log(JSON.stringify({camera:i+1,pushes:pushes.length,frames:frames.length,pushGapMedian:percentile(gaps,.5),pushGap95:percentile(gaps,.95),ageMedian:percentile(pushes.map(x=>x.ageMs),.5),age95:percentile(pushes.map(x=>x.ageMs),.95),headroomMedian:percentile(headroom,.5),headroom05:percentile(headroom,.05),predictionPercent:100*prediction/frames.length,expected,missing,longestEmptyRun,bridgedFrames,drawnBridgedFrames,
    measuredSamples: measuredCentreErrors.length, measuredCentreError95Px:percentile(measuredCentreErrors,.95), measuredSizeError95Px:percentile(measuredSizeErrors,.95),
    measuredCentreErrorMaxPx:Math.max(0,...measuredCentreErrors), measuredSizeErrorMaxPx:Math.max(0,...measuredSizeErrors),riskGaps}))
}
