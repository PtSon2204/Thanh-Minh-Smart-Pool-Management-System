import { useEffect, useRef, useState } from 'react'
import { Alert, Button, Spin, Tag } from 'antd'
import { StopOutlined, VideoCameraOutlined } from '@ant-design/icons'

function webcamError(error) {
  if (['NotAllowedError', 'SecurityError'].includes(error?.name)) return 'Chưa có quyền camera. Cho phép website sử dụng camera trong phần quyền của trình duyệt rồi thử lại.'
  if (['NotFoundError', 'DevicesNotFoundError'].includes(error?.name)) return 'Không tìm thấy webcam. Kiểm tra camera máy tính hoặc kết nối camera USB.'
  if (['NotReadableError', 'TrackStartError', 'AbortError'].includes(error?.name)) return 'Không mở được webcam. Kiểm tra quyền camera của Windows và đóng ứng dụng khác đang dùng camera.'
  return 'Không thể mở camera máy tính. Vui lòng thử lại.'
}

export default function BrowserCameraPreview({ name = 'máy tính' }) {
  const videoRef = useRef(null)
  const streamRef = useRef(null)
  const requestRef = useRef(0)
  const mounted = useRef(false)
  const [state, setState] = useState('idle')
  const [error, setError] = useState('')

  function release() {
    requestRef.current++
    const stream = streamRef.current
    streamRef.current = null
    stream?.getTracks().forEach(track => { track.onended = null; track.stop() })
    if (videoRef.current) { videoRef.current.pause(); videoRef.current.srcObject = null }
  }

  useEffect(() => {
    mounted.current = true
    function stopWhenLeaving() {
      release()
      if (mounted.current) setState('idle')
    }
    window.addEventListener('pagehide', stopWhenLeaving)
    return () => {
      mounted.current = false
      window.removeEventListener('pagehide', stopWhenLeaving)
      release()
    }
  }, [])

  async function start() {
    release()
    setError('')
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
      setState('error')
      setError('Mở trang bằng localhost hoặc HTTPS để trình duyệt cho phép sử dụng webcam.')
      return
    }
    setState('opening')
    const request = requestRef.current
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false })
      if (!mounted.current || request !== requestRef.current) { stream.getTracks().forEach(track => track.stop()); return }
      streamRef.current = stream
      stream.getVideoTracks().forEach(track => {
        track.onended = () => {
          release()
          if (mounted.current) { setState('error'); setError('Camera đã ngừng hoạt động hoặc quyền truy cập đã bị thu hồi.') }
        }
      })
      videoRef.current.srcObject = stream
      await videoRef.current.play()
      if (mounted.current && request === requestRef.current) setState('playing')
    } catch (cause) {
      if (!mounted.current || request !== requestRef.current) return
      release()
      setState('error')
      setError(webcamError(cause))
    }
  }

  function stop() { release(); setError(''); setState('idle') }

  return <section className="browser-camera-preview" aria-label="Xem thử camera máy tính">
    <Alert type="info" showIcon title="Camera trên máy đang mở trang" description="Hình ảnh chỉ xem trực tiếp trong trình duyệt này. Không gửi video lên server; mở trang trên máy khác sẽ dùng webcam của máy đó." />
    <div className="camera-view-status" role="status"><Tag color={state === 'playing' ? 'success' : 'default'}>{state === 'playing' ? 'Webcam đang hoạt động' : state === 'opening' ? 'Đang mở webcam...' : 'Camera máy tính'}</Tag></div>
    <div className="camera-video-container">
      <video ref={videoRef} autoPlay muted playsInline className={state === 'playing' ? 'camera-video' : 'camera-video camera-video-hidden'} aria-label={`Webcam ${name}`} />
      {state !== 'playing' && <div className="camera-video-loading">{state === 'opening' ? <><Spin /><span>Cho phép truy cập camera khi trình duyệt yêu cầu.</span></> : <><VideoCameraOutlined /><span>Bấm “Mở webcam” để xem thử.</span></>}</div>}
    </div>
    {error && <Alert type="error" showIcon title={error} />}
    <div className="browser-camera-actions">
      <Button type="primary" icon={<VideoCameraOutlined />} disabled={state === 'opening' || state === 'playing'} onClick={start}>{state === 'error' ? 'Thử lại webcam' : 'Mở webcam'}</Button>
      {['opening', 'playing'].includes(state) && <Button icon={<StopOutlined />} onClick={stop}>Dừng camera</Button>}
    </div>
  </section>
}
