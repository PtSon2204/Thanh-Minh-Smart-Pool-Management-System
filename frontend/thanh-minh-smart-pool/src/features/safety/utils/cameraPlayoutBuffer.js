// Owns decoded VideoFrames until presentation. Capture and presentation keep
// running at video FPS; a short bounded delay gives AI time to bracket the frame.
export class CameraPlayoutBuffer {
  pending = []
  current = null
  delay = 300
  lastTake = null
  constructor(capacity = 18) { this.capacity = capacity }

  add(frame, timestamp, receivedAt) {
    this.pending.push({ frame, timestamp, receivedAt })
    while (this.pending.length > this.capacity) this.pending.shift().frame.close()
  }

  take(now, delay) {
    const elapsed = this.lastTake === null ? 0 : Math.max(0, now - this.lastTake)
    this.lastTake = now
    // Change latency gradually while playing, instead of freezing/jumping each
    // time a new AI cost estimate arrives. First presentation uses the target.
    const target = Number.isFinite(delay) ? Math.max(0, Math.min(300, delay)) : 300
    this.delay = this.current && elapsed < 500
      ? Math.max(this.delay - elapsed * .1, Math.min(this.delay + elapsed * .1, target)) : target
    let next = null
    // If a high-resolution/high-FPS source fills the queue, prefer a smaller
    // delay over retaining unbounded frames or waiting forever for the deadline.
    while (this.pending.length && (now - this.pending[0].receivedAt >= this.delay || this.pending.length >= this.capacity)) {
      next?.frame.close()
      next = this.pending.shift()
    }
    if (next) {
      this.current?.frame.close()
      this.current = next
    }
    return next
  }

  clear() {
    this.pending.forEach(item => item.frame.close())
    this.current?.frame.close()
    this.pending = []
    this.current = null
    this.lastTake = null
  }

  // RTP of the frame currently held for presentation, and of the next queued one.
  // Overlay glides box sampling between them on the real-time clock.
  currentRtp() { return this.current?.timestamp }
  peekNextRtp() { return this.pending[0]?.timestamp }
}
