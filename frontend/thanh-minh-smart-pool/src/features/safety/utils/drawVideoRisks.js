export function drawVideoRisks(canvas, video, risks) {
  if (!canvas || !video) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const width = video.clientWidth
  const height = video.clientHeight
  const ratio = window.devicePixelRatio || 1
  if (canvas.width !== Math.round(width * ratio) || canvas.height !== Math.round(height * ratio)) {
    canvas.width = Math.round(width * ratio)
    canvas.height = Math.round(height * ratio)
  }
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0)
  ctx.clearRect(0, 0, width, height)
  if (!video.videoWidth || !video.videoHeight) return
  const scale = Math.min(width / video.videoWidth, height / video.videoHeight)
  const imageWidth = video.videoWidth * scale
  const imageHeight = video.videoHeight * scale
  const left = (width - imageWidth) / 2
  const top = (height - imageHeight) / 2
  for (const risk of risks) {
    const [x1, y1, x2, y2] = risk.box || []
    if (![x1, y1, x2, y2].every(Number.isFinite) || x2 <= x1 || y2 <= y1) continue
    const color = risk.severity === 'CRITICAL' ? '#EF4444' : '#F97316'
    const x = left + x1 * imageWidth
    const y = top + y1 * imageHeight
    ctx.strokeStyle = color
    ctx.lineWidth = 3
    ctx.strokeRect(x, y, (x2 - x1) * imageWidth, (y2 - y1) * imageHeight)
    const label = risk.severity === 'CRITICAL' ? 'NGUY HIỂM' : 'CHÚ Ý'
    ctx.font = 'bold 13px system-ui'
    ctx.fillStyle = color
    const labelY = Math.max(0, y - 21)
    ctx.fillRect(x, labelY, ctx.measureText(label).width + 12, 21)
    ctx.fillStyle = '#fff'
    ctx.fillText(label, x + 6, labelY + 15)
  }
}
