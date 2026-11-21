import { useState, useEffect, useCallback } from 'react'
import alarmSrc from '../../../assets/am-thanh-cuu-ho.mp3'

// Module-level singleton để AdminLayout và ActivateAlertPage
// dùng chung 1 luồng phát duy nhất, tránh xung đột.
let currentAudio = null
let audioCtx = null
let sirenInterval = null
let vibrateInterval = null
let globalIsPlaying = false
const listeners = new Set()

function notifyListeners() {
  listeners.forEach((fn) => {
    try {
      fn(globalIsPlaying)
    } catch {
      // ignore listener error
    }
  })
}

function getAudio() {
  if (!currentAudio) {
    currentAudio = new Audio(alarmSrc)
    currentAudio.preload = 'auto'
    currentAudio.loop = true
    currentAudio.volume = 1.0
  }
  return currentAudio
}

function getAudioContext() {
  if (!audioCtx) {
    const CtxClass = window.AudioContext || window.webkitAudioContext
    if (CtxClass) {
      audioCtx = new CtxClass()
    }
  }
  if (audioCtx && audioCtx.state === 'suspended') {
    audioCtx.resume().catch(() => {})
  }
  return audioCtx
}

// Còi dự phòng Web Audio API (phát ra khi thiết bị chặn MP3)
function playBeep(freq, duration) {
  try {
    const ctx = getAudioContext()
    if (!ctx) return
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'sawtooth'
    osc.frequency.setValueAtTime(freq, ctx.currentTime)

    gain.gain.setValueAtTime(0.8, ctx.currentTime)
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + duration)

    osc.connect(gain)
    gain.connect(ctx.destination)

    osc.start(ctx.currentTime)
    osc.stop(ctx.currentTime + duration)
  } catch (e) {
    console.warn('[useAlarmSound] beep error:', e)
  }
}

function startOscillatorSiren() {
  if (sirenInterval) return
  let toggle = false
  playBeep(900, 0.35)
  sirenInterval = setInterval(() => {
    if (!globalIsPlaying) return
    playBeep(toggle ? 980 : 620, 0.35)
    toggle = !toggle
  }, 380)
}

function stopOscillatorSiren() {
  if (sirenInterval) {
    clearInterval(sirenInterval)
    sirenInterval = null
  }
}

// Rung điện thoại khi có cảnh báo khẩn cấp (cho thiết bị di động)
function startVibration() {
  if (typeof navigator !== 'undefined' && navigator.vibrate) {
    try {
      navigator.vibrate([500, 200, 500, 200, 500, 300])
      if (!vibrateInterval) {
        vibrateInterval = setInterval(() => {
          if (!globalIsPlaying) {
            clearInterval(vibrateInterval)
            vibrateInterval = null
            return
          }
          navigator.vibrate([500, 200, 500, 200, 500, 300])
        }, 2500)
      }
    } catch {
      // ignore vibration error
    }
  }
}

function stopVibration() {
  if (vibrateInterval) {
    clearInterval(vibrateInterval)
    vibrateInterval = null
  }
  if (typeof navigator !== 'undefined' && navigator.vibrate) {
    try {
      navigator.vibrate(0)
    } catch {
      // ignore
    }
  }
}

// Tự động mở khóa AudioContext âm thầm khi người dùng bấm bất kỳ đâu trong app
if (typeof window !== 'undefined') {
  const silentUnlock = () => {
    try {
      const ctx = getAudioContext()
      if (ctx && ctx.state === 'suspended') {
        ctx.resume().catch(() => {})
      }
    } catch {
      // ignore unlock error
    }
  }
  window.addEventListener('click', silentUnlock, { once: true, passive: true })
  window.addEventListener('touchstart', silentUnlock, { once: true, passive: true })
}

/**
 * Bắt đầu phát âm thanh cảnh báo (file MP3, dự phòng còi Web Audio).
 */
export function playGlobalAlarm(level = 'EMERGENCY') {
  globalIsPlaying = true
  notifyListeners()
  startVibration()

  try {
    const audio = getAudio()
    audio.loop = true
    audio.volume = level === 'EMERGENCY' ? 1.0 : 0.8

    const promise = audio.play()
    if (promise !== undefined) {
      promise.catch((err) => {
        console.warn('[useAlarmSound] MP3 play blocked/failed, using fallback siren:', err)
        startOscillatorSiren()
      })
    }
  } catch (err) {
    console.warn('[useAlarmSound] Audio error, using fallback siren:', err)
    startOscillatorSiren()
  }
}

/**
 * Dừng âm thanh cảnh báo.
 */
export function stopGlobalAlarm() {
  globalIsPlaying = false
  notifyListeners()
  stopVibration()

  if (currentAudio) {
    try {
      currentAudio.pause()
      currentAudio.currentTime = 0
    } catch {
      // ignore pause error
    }
  }
  stopOscillatorSiren()
}

/**
 * React Hook sử dụng trong toàn bộ hệ thống
 */
export function useAlarmSound() {
  const [isPlaying, setIsPlaying] = useState(globalIsPlaying)

  useEffect(() => {
    const listener = (playing) => setIsPlaying(playing)
    listeners.add(listener)
    return () => {
      listeners.delete(listener)
    }
  }, [])

  const startAlarm = useCallback((level = 'EMERGENCY') => {
    playGlobalAlarm(level)
  }, [])

  const stopAlarm = useCallback(() => {
    stopGlobalAlarm()
  }, [])

  return {
    isPlaying,
    startAlarm,
    stopAlarm,
  }
}
