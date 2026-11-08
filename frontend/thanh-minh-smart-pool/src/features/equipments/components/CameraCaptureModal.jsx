import { useEffect, useRef, useState, useCallback } from 'react'
import { Modal, Button, Alert, Spin, Tag, Space, message } from 'antd'
import {
  CameraOutlined,
  SyncOutlined,
  CheckOutlined,
  WarningOutlined,
  LoadingOutlined,
} from '@ant-design/icons'

export default function CameraCaptureModal({
  open,
  onClose,
  onCapture,
  title = 'Chụp ảnh thiết bị',
  maxPhotos = 5,
  currentCount = 0,
  captureHint = 'Hướng camera vào thiết bị rồi bấm nút chụp',
}) {
  const videoRef = useRef(null)
  const streamRef = useRef(null)
  const requestRef = useRef(0)
  const mountedRef = useRef(false)

  const [facingMode, setFacingMode] = useState('environment') // 'environment' (sau) hoặc 'user' (trước)
  const [isStarting, setIsStarting] = useState(false)
  const [cameraError, setCameraError] = useState(null)
  const [sessionCount, setSessionCount] = useState(0)
  const [isSnapping, setIsSnapping] = useState(false)
  const [showFlash, setShowFlash] = useState(false)

  // Dừng stream và giải phóng phần cứng camera an toàn
  const stopStream = useCallback(() => {
    requestRef.current++
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((track) => {
        track.onended = null
        track.stop()
      })
      streamRef.current = null
    }
    if (videoRef.current) {
      try {
        videoRef.current.pause()
      } catch {
        // bỏ qua lỗi nếu video chưa bắt đầu
      }
      videoRef.current.srcObject = null
    }
  }, [])

  // Khởi động camera với fallback và cơ chế chống xung đột Chromium AbortError
  const startStream = useCallback(
    async (mode) => {
      stopStream()
      const requestId = requestRef.current

      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        setCameraError(
          'Trình duyệt hoặc kết nối hiện tại không hỗ trợ truy cập Camera (yêu cầu localhost hoặc HTTPS).'
        )
        return
      }

      setCameraError(null)
      setIsStarting(true)

      try {
        let stream
        try {
          // Ưu tiên độ phân giải HD và chế độ xoay camera (ideal để không bị OverconstrainedError)
          stream = await navigator.mediaDevices.getUserMedia({
            video: {
              facingMode: { ideal: mode },
              width: { ideal: 1920 },
              height: { ideal: 1080 },
            },
            audio: false,
          })
        } catch {
          // Fallback cho webcam laptop / máy tính để bàn / camera USB cơ bản
          stream = await navigator.mediaDevices.getUserMedia({
            video: true,
            audio: false,
          })
        }

        // Kiểm tra nếu component đã unmount hoặc có yêu cầu stream mới hơn
        if (!mountedRef.current || requestId !== requestRef.current) {
          stream.getTracks().forEach((track) => track.stop())
          return
        }

        streamRef.current = stream

        stream.getVideoTracks().forEach((track) => {
          track.onended = () => {
            if (!mountedRef.current || requestId !== requestRef.current) return
            stopStream()
            setCameraError('Camera đã ngắt kết nối hoặc quyền truy cập đã bị thu hồi.')
          }
        })

        if (videoRef.current) {
          videoRef.current.srcObject = stream
          try {
            await videoRef.current.play()
          } catch (playErr) {
            // Lỗi AbortError xảy ra khi Chromium hủy lệnh play() cũ do stream mới nạp; an toàn bỏ qua
            if (playErr.name === 'AbortError') {
              return
            }
            throw playErr
          }
        }

        if (mountedRef.current && requestId === requestRef.current) {
          setIsStarting(false)
        }
      } catch (err) {
        if (!mountedRef.current || requestId !== requestRef.current) return
        stopStream()
        setIsStarting(false)

        if (err.name === 'AbortError') {
          return
        }

        if (
          err.name === 'NotAllowedError' ||
          err.name === 'PermissionDeniedError' ||
          err.name === 'SecurityError'
        ) {
          setCameraError(
            'Bạn chưa cấp quyền truy cập Camera. Vui lòng cho phép quyền Camera trên thanh địa chỉ trình duyệt rồi bấm Thử lại.'
          )
        } else if (
          err.name === 'NotFoundError' ||
          err.name === 'DevicesNotFoundError'
        ) {
          setCameraError(
            'Không tìm thấy thiết bị Camera / Webcam trên máy tính hoặc điện thoại của bạn.'
          )
        } else if (
          err.name === 'NotReadableError' ||
          err.name === 'TrackStartError'
        ) {
          setCameraError(
            'Camera đang bị ứng dụng khác sử dụng (Zoom, Teams, Zalo, Google Meet...). Vui lòng đóng ứng dụng đó rồi bấm Thử lại.'
          )
        } else {
          setCameraError(
            `Không thể khởi động Camera: ${err.message || 'Lỗi không xác định'}`
          )
        }
      }
    },
    [stopStream]
  )

  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
      stopStream()
    }
  }, [stopStream])

  // Camera stream: gọi startStream/stopStream trong effect để đồng bộ trạng thái camera với prop `open`
  useEffect(() => {
    if (open) {
      startStream(facingMode) // eslint-disable-line react-hooks/set-state-in-effect
    } else {
      stopStream()
    }
  }, [open, facingMode, startStream, stopStream])

  // Reset sessionCount khi modal đóng — cleanup state hợp lệ
  useEffect(() => {
    if (!open) {
      setSessionCount(0) // eslint-disable-line react-hooks/set-state-in-effect
    }
  }, [open])

  const toggleCamera = () => {
    const nextMode = facingMode === 'environment' ? 'user' : 'environment'
    setFacingMode(nextMode)
  }

  // Chụp ảnh liên tục nhiều lần mà không làm gián đoạn hay tải lại stream video
  const handleSnap = async () => {
    if (!videoRef.current || isStarting || cameraError || isSnapping) return

    if (currentCount >= maxPhotos) {
      message.warning(`Đã đạt giới hạn tối đa ${maxPhotos} ảnh cho thiết bị này.`)
      return
    }

    try {
      setIsSnapping(true)
      const video = videoRef.current
      const canvas = document.createElement('canvas')
      canvas.width = video.videoWidth || 1280
      canvas.height = video.videoHeight || 720
      const ctx = canvas.getContext('2d')
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height)

      // Hiệu ứng chớp sáng mô phỏng màn trập chụp ảnh
      setShowFlash(true)
      setTimeout(() => setShowFlash(false), 200)

      canvas.toBlob(
        async (blob) => {
          if (!blob) {
            message.error('Không thể tạo file ảnh từ camera.')
            setIsSnapping(false)
            return
          }

          const file = new File([blob], `capture_${Date.now()}.jpg`, {
            type: 'image/jpeg',
          })

          try {
            await onCapture(file)
            setSessionCount((prev) => prev + 1)
          } catch (err) {
            message.error(err.message || 'Lỗi tải ảnh lên hệ thống.')
          } finally {
            setIsSnapping(false)
          }
        },
        'image/jpeg',
        0.92
      )
    } catch {
      setIsSnapping(false)
      message.error('Lỗi khi chụp khung hình từ camera.')
    }
  }

  const handleClose = () => {
    stopStream()
    onClose()
  }

  const reachedLimit = currentCount >= maxPhotos

  return (
    <Modal
      title={
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            paddingRight: 32,
          }}
        >
          <Space>
            <CameraOutlined style={{ color: '#0068ff', fontSize: 20 }} />
            <span style={{ fontWeight: 600 }}>{title}</span>
          </Space>
          <Tag color={reachedLimit ? 'error' : 'blue'}>
            Đã chụp: {currentCount} / {maxPhotos} ảnh
          </Tag>
        </div>
      }
      open={open}
      onCancel={handleClose}
      width={720}
      footer={
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}
        >
          <span style={{ fontSize: 13, color: '#64748b' }}>
            {sessionCount > 0
              ? `Đã chụp thêm ${sessionCount} ảnh mới trong phiên này`
              : captureHint}
          </span>
          <Button type="primary" icon={<CheckOutlined />} onClick={handleClose}>
            Hoàn tất ({currentCount} ảnh)
          </Button>
        </div>
      }
    >
      <div
        style={{
          position: 'relative',
          background: '#090d16',
          borderRadius: 12,
          overflow: 'hidden',
          minHeight: 380,
          aspectRatio: '16 / 9',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        {/* Khung video live stream: LUÔN giữ trong DOM để Chromium không hủy layout box */}
        <video
          ref={videoRef}
          autoPlay
          playsInline
          muted
          style={{
            width: '100%',
            height: '100%',
            objectFit: 'contain',
            visibility: isStarting || cameraError ? 'hidden' : 'visible',
          }}
        />

        {/* Khung ngắm chụp ảnh (Viewfinder grid) */}
        {!cameraError && !isStarting && (
          <div
            style={{
              position: 'absolute',
              top: '12%',
              left: '12%',
              right: '12%',
              bottom: '12%',
              border: '2px dashed rgba(255,255,255,0.4)',
              borderRadius: 12,
              pointerEvents: 'none',
              boxShadow: '0 0 0 9999px rgba(0, 0, 0, 0.25)',
            }}
          />
        )}

        {/* Nút lật camera trước / sau */}
        {!cameraError && !isStarting && (
          <Button
            shape="circle"
            icon={<SyncOutlined />}
            onClick={toggleCamera}
            style={{
              position: 'absolute',
              top: 16,
              right: 16,
              background: 'rgba(0,0,0,0.6)',
              color: '#fff',
              border: '1px solid rgba(255,255,255,0.3)',
              zIndex: 10,
            }}
            title="Đổi camera trước/sau"
          />
        )}

        {/* Hiệu ứng chớp sáng khi chụp (Flash animation) */}
        {showFlash && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              background: 'rgba(255,255,255,0.85)',
              zIndex: 20,
              pointerEvents: 'none',
            }}
          />
        )}

        {/* Trạng thái đang khởi động: Hiển thị đè overlay lên trên video */}
        {isStarting && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              background: '#090d16',
              zIndex: 5,
              color: '#fff',
            }}
          >
            <Spin size="large" />
            <div style={{ marginTop: 16, fontSize: 14 }}>
              Đang kết nối Camera máy tính / điện thoại...
            </div>
          </div>
        )}

        {/* Lỗi truy cập Camera */}
        {cameraError && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              justifyContent: 'center',
              padding: 24,
              background: '#090d16',
              zIndex: 6,
            }}
          >
            <Alert
              type="error"
              showIcon
              icon={<WarningOutlined style={{ fontSize: 24 }} />}
              message="Không thể mở Camera"
              description={cameraError}
              style={{ maxWidth: 500 }}
            />
            <div style={{ textAlign: 'center', marginTop: 16 }}>
              <Button
                type="primary"
                icon={<SyncOutlined />}
                onClick={() => startStream(facingMode)}
              >
                Thử kết nối lại
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Nút chụp ảnh lớn dưới video */}
      {!cameraError && (
        <div style={{ textAlign: 'center', marginTop: 20 }}>
          <button
            type="button"
            onClick={handleSnap}
            disabled={isStarting || reachedLimit || isSnapping}
            style={{
              width: 72,
              height: 72,
              borderRadius: '50%',
              background: reachedLimit ? '#94a3b8' : isSnapping ? '#f59e0b' : '#ef4444',
              border: '4px solid #ffffff',
              boxShadow: isSnapping
                ? '0 4px 14px rgba(245, 158, 11, 0.4)'
                : '0 4px 14px rgba(239, 68, 68, 0.4)',
              cursor: reachedLimit || isSnapping ? 'not-allowed' : 'pointer',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              transition: 'transform 0.15s ease, background-color 0.2s ease',
              outline: 'none',
            }}
            onMouseDown={(e) => {
              if (!reachedLimit && !isSnapping) {
                e.currentTarget.style.transform = 'scale(0.92)'
              }
            }}
            onMouseUp={(e) => (e.currentTarget.style.transform = 'scale(1)')}
          >
            {isSnapping ? (
              <LoadingOutlined style={{ color: '#fff', fontSize: 28 }} />
            ) : (
              <CameraOutlined style={{ color: '#fff', fontSize: 30 }} />
            )}
          </button>
          <div
            style={{
              marginTop: 8,
              fontSize: 13,
              fontWeight: 500,
              color: reachedLimit ? '#ef4444' : '#334155',
            }}
          >
            {isSnapping
              ? 'Đang xử lý ảnh...'
              : reachedLimit
              ? `Đã đủ ${maxPhotos} ảnh tối đa`
              : maxPhotos === 1 ? 'Bấm để chụp ảnh' : 'Bấm để chụp ảnh (chụp liên tiếp nhiều ảnh)'}
          </div>
        </div>
      )}
    </Modal>
  )
}
