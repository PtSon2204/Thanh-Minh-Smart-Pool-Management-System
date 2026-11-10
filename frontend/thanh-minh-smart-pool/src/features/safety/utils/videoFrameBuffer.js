export class VideoFrameBuffer {
  fps = 0
  readyFrames = 0
  complete = false
  frames = new Map()

  merge(window, position) {
    if (!Number.isFinite(window.fps) || window.fps <= 0) return
    if (this.fps && this.fps !== window.fps) this.frames.clear()
    this.fps = window.fps
    this.readyFrames = Math.max(this.readyFrames, window.readyFrames)
    this.complete ||= window.complete
    window.frames.forEach((risks, offset) => this.frames.set(window.startFrame + offset, risks))
    const at = this.index(position)
    for (const key of this.frames.keys()) {
      if (key < at - this.fps || key > at + this.fps * 8) this.frames.delete(key)
    }
  }

  index(seconds) { return Math.floor(seconds * this.fps + 0.000001) }

  sample(seconds) {
    if (!this.fps || !Number.isFinite(seconds) || seconds < 0) return null
    const index = this.index(seconds)
    if (this.complete && index >= this.readyFrames) return { index, risks: [] }
    return this.frames.has(index) ? { index, risks: this.frames.get(index) } : null
  }

  ahead(seconds) {
    if (!this.sample(seconds)) return 0
    let end = this.index(seconds)
    while (this.frames.has(end)) end++
    if (this.complete && end >= this.readyFrames) return Infinity
    return Math.max(0, end / this.fps - seconds)
  }
}

export function videoWarningKey(risk) {
  return `${risk.trackId}:${risk.episode}:${risk.severity}`
}
