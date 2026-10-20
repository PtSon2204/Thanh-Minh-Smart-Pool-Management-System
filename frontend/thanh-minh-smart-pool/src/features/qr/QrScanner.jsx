import { useCallback, useLayoutEffect, useRef, useState } from 'react'
import { Alert, Button } from 'antd'
import { Scanner } from '@yudiel/react-qr-scanner'
import { parseQrPayload } from './qrPurpose'

const QR_FORMATS = ['qr_code']

function stopTracks(container) {
  container?.querySelectorAll('video').forEach((video) => {
    video.srcObject?.getTracks().forEach((track) => track.stop())
    video.pause()
    video.srcObject = null
  })
}

function ActiveQrScanner({ expectedPurpose, onDecoded, onError, ScannerComponent }) {
  const containerRef = useRef(null)
  const acceptedRef = useRef(false)
  const [cameraRequested, setCameraRequested] = useState(false)
  const [acceptedPayload, setAcceptedPayload] = useState(null)
  const [scannerError, setScannerError] = useState(null)

  const releaseCamera = useCallback(() => {
    stopTracks(containerRef.current)
  }, [])

  const resetScanner = useCallback(() => {
    releaseCamera()
    acceptedRef.current = false
    setAcceptedPayload(null)
    setScannerError(null)
    setCameraRequested(true)
  }, [releaseCamera])

  useLayoutEffect(() => () => releaseCamera(), [releaseCamera])

  const reject = useCallback((message) => {
    releaseCamera()
    setCameraRequested(false)
    setScannerError(message)
  }, [releaseCamera])

  const handleScan = useCallback((detectedCodes) => {
    if (acceptedRef.current) return

    const decodedPayloads = [...new Set(detectedCodes
      .map((code) => code.rawValue)
      .filter((payload) => typeof payload === 'string'))]

    if (decodedPayloads.length === 0) return
    if (decodedPayloads.length > 1) {
      reject('Máy ảnh nhìn thấy nhiều mã QR. Chỉ giữ một mã trong khung hình rồi quét lại.')
      return
    }

    const result = parseQrPayload(decodedPayloads[0], expectedPurpose)
    if (!result.accepted) {
      reject(result.reason)
      return
    }

    acceptedRef.current = true
    releaseCamera()
    setCameraRequested(false)
    setAcceptedPayload(result.payload)
    onDecoded(result.payload)
  }, [expectedPurpose, onDecoded, reject, releaseCamera])

  const handleScannerError = useCallback((error) => {
    if (acceptedRef.current) return

    releaseCamera()
    setCameraRequested(false)
    setScannerError(error?.message || 'Không thể mở máy ảnh. Hãy kiểm tra quyền máy ảnh và kết nối bảo mật.')
    onError?.(error)
  }, [onError, releaseCamera])

  const scannerVisible = cameraRequested && !acceptedPayload && !scannerError

  return <section ref={containerRef} aria-label="Quét mã QR">
    {!scannerVisible && !acceptedPayload && !scannerError && <Button type="primary" onClick={resetScanner}>Mở máy ảnh để quét QR</Button>}
    {scannerVisible && <ScannerComponent
      allowMultiple={false}
      formats={QR_FORMATS}
      onError={handleScannerError}
      onScan={handleScan}
    />}
    {acceptedPayload && <Alert
      type="success"
      showIcon
      title="Đã đọc mã QR"
      description={<><code>{acceptedPayload}</code><div><Button type="link" onClick={resetScanner}>Quét lại</Button></div></>}
    />}
    {scannerError && <Alert
      type="error"
      showIcon
      title="Không thể quét mã QR"
      description={<><span>{scannerError}</span><div><Button type="link" onClick={resetScanner}>Quét lại</Button></div></>}
    />}
  </section>
}

export function QrScanner({ expectedPurpose, onDecoded, onError, open = true, ScannerComponent = Scanner }) {
  if (!open) return null

  return <ActiveQrScanner expectedPurpose={expectedPurpose} onDecoded={onDecoded} onError={onError} ScannerComponent={ScannerComponent} />
}

export default QrScanner
