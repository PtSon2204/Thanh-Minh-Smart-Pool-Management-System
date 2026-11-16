const ticks = (a, b) => (a - b) | 0
const clamp = (value, min, max) => Math.max(min, Math.min(max, value))
const geometry = box => [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2, box[2] - box[0], box[3] - box[1]]

function tangent(left, right, leftGap, rightGap) {
  if (left * right <= 0) return 0
  const a = 2 * rightGap + leftGap, b = rightGap + 2 * leftGap
  return (a + b) / (a / left + b / right)
}

// Interpolate on the displayed RTP timeline instead of filtering yesterday's
// displayed position. Every measured endpoint stays exact, without EMA phase lag.
export function interpolateCameraBox(before, after, progress, previous, following) {
  const t = clamp(progress, 0, 1)
  if (t === 0) return before.box
  if (t === 1) return after.box
  const duration = ticks(after.timestamp, before.timestamp)
  const previousGap = previous && ticks(before.timestamp, previous.timestamp)
  const followingGap = following && ticks(following.timestamp, after.timestamp)
  const hasPrevious = previousGap > 0 && previousGap <= 90000
  const hasFollowing = followingGap > 0 && followingGap <= 90000
  if (duration <= 0 || !hasPrevious && !hasFollowing)
    return before.box.map((value, index) => value + (after.box[index] - value) * t)
  const start = geometry(before.box), end = geometry(after.box)
  const earlier = hasPrevious ? geometry(previous.box) : null
  const later = hasFollowing ? geometry(following.box) : null
  const values = start.map((value, index) => {
    const slope = (end[index] - value) / duration
    if (slope === 0) return value
    let first = earlier ? tangent((value - earlier[index]) / previousGap, slope, previousGap, duration) : slope
    let last = later ? tangent(slope, (later[index] - end[index]) / followingGap, duration, followingGap) : slope
    const magnitude = Math.hypot(first / slope, last / slope)
    if (magnitude > 3) { first *= 3 / magnitude; last *= 3 / magnitude }
    const t2 = t * t, t3 = t2 * t
    const result = (2 * t3 - 3 * t2 + 1) * value + (t3 - 2 * t2 + t) * duration * first
      + (-2 * t3 + 3 * t2) * end[index] + (t3 - t2) * duration * last
    return clamp(result, Math.min(value, end[index]), Math.max(value, end[index]))
  })
  const width = clamp(values[2], .000001, 1), height = clamp(values[3], .000001, 1)
  const x = clamp(values[0], width / 2, 1 - width / 2), y = clamp(values[1], height / 2, 1 - height / 2)
  return [x - width / 2, y - height / 2, x + width / 2, y + height / 2]
}
