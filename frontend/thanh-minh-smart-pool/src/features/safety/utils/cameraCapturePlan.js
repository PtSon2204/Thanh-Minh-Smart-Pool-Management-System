// Size retained frames for the displayed image and the required lookahead.
// A fixed six-frame cap at 720p only retains 200 ms at 25 FPS, defeating a
// 400–550 ms interpolation buffer. Scale retained pixels before dropping time.
export function cameraCapturePlan(videoWidth, videoHeight, width, height, pixelRatio, frameMs, delay) {
  if (!(videoWidth > 0 && videoHeight > 0 && width > 0 && height > 0)) return null
  const interval = Number.isFinite(frameMs) && frameMs >= 8 && frameMs <= 1000 ? frameMs : 40
  const lookahead = Number.isFinite(delay) ? Math.max(160, Math.min(300, delay)) : 300
  const capacity = Math.min(40, Math.max(4, Math.ceil((lookahead + 80) / interval) + 2))
  const ratio = Number.isFinite(pixelRatio) ? Math.max(1, Math.min(2, pixelRatio)) : 1
  const fit = Math.min(1, width * ratio / videoWidth, height * ratio / videoHeight)
  const budget = 48 * 1024 * 1024 // conservative RGBA estimate across pending frames + current
  const memoryScale = Math.sqrt(budget / ((capacity + 1) * videoWidth * videoHeight * 4))
  const scale = Math.min(fit, memoryScale)
  return { width: Math.max(1, Math.floor(videoWidth * scale)), height: Math.max(1, Math.floor(videoHeight * scale)), capacity }
}
