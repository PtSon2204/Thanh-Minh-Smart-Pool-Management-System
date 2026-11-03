import { useCallback, useEffect, useState } from 'react'
import { QrcodeOutlined, SearchOutlined, UsergroupAddOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Descriptions, Input, Segmented, Space, Spin } from 'antd'
import { QrScanner } from "../../../features/qr/components/QrScanner"
import { QR_PURPOSE } from "../../../features/qr/utils/qrPurpose"
import poolAccessService from "../../../features/tickets/services/poolAccessService"
import { hasAcceptedEntry, prepareTicketCode, shouldReconcileConfirmation, vietnamDate } from "../../../features/tickets/utils/poolAccessState"
import { ServiceListHeader } from "../../../features/services/components/ServiceListChrome"
import "../../../features/services/styles/serviceManagement.css"

const INPUT_MODE = Object.freeze({ qr: 'Qr', manual: 'Manual' })

function valueOf(object, camelCase, pascalCase) {
  return object?.[camelCase] ?? object?.[pascalCase]
}

function resultMessage(result, fallback) {
  return valueOf(result, 'message', 'Message') || valueOf(result, 'reason', 'Reason') || fallback
}

function ticketOf(result) {
  return valueOf(result, 'ticket', 'Ticket')
}

function TicketPreview({ result }) {
  const ticket = ticketOf(result)
  if (!ticket) return null

  const usedAt = valueOf(ticket, 'usedAt', 'UsedAt')
  const remaining = valueOf(ticket, 'remainingEntries', 'RemainingEntries')
  const category = valueOf(ticket, 'category', 'Category')
  const expiryDate = valueOf(ticket, 'expiryDate', 'ExpiryDate')

  return (
    <Descriptions bordered column={1} size="small" title="Thông tin vé">
      <Descriptions.Item label="Loại vé">{valueOf(ticket, 'ticketTypeName', 'TicketTypeName') || 'Không xác định'}</Descriptions.Item>
      <Descriptions.Item label="Phân loại">{category || 'Không xác định'}</Descriptions.Item>
      <Descriptions.Item label="Trạng thái">{valueOf(ticket, 'status', 'Status') || 'Không xác định'}</Descriptions.Item>
      <Descriptions.Item label="Thời điểm sử dụng">{usedAt ? new Date(usedAt).toLocaleString('vi-VN') : 'Chưa sử dụng'}</Descriptions.Item>
      <Descriptions.Item label="Hết hạn">
        {expiryDate
          ? new Date(expiryDate).toLocaleString('vi-VN')
          : (category === 'VE_LUOT' ? 'Không giới hạn (dùng hết số lượt)' : 'Không áp dụng')}
      </Descriptions.Item>
      <Descriptions.Item label="Lượt còn lại">
        {remaining !== undefined && remaining !== null ? (
          <strong style={{ color: remaining > 0 ? '#1677ff' : '#ff4d4f', fontSize: 14 }}>
            {remaining} lượt
          </strong>
        ) : 'Không áp dụng'}
      </Descriptions.Item>
    </Descriptions>
  )
}

export default function TicketScannerPage() {
  const [source, setSource] = useState(INPUT_MODE.qr)
  const [manualCode, setManualCode] = useState('')
  const [result, setResult] = useState(null)
  const [phase, setPhase] = useState('idle')
  const [notice, setNotice] = useState(null)
  const [scannerKey, setScannerKey] = useState(0)
  const [scannerOpen, setScannerOpen] = useState(true)
  const [countdown, setCountdown] = useState(null)
  const [countdownPaused, setCountdownPaused] = useState(false)

  const reset = useCallback(() => {
    setScannerOpen(true)
    setScannerKey((current) => current + 1)
    setManualCode('')
    setResult(null)
    setPhase('idle')
    setNotice(null)
    setCountdown(null)
    setCountdownPaused(false)
  }, [])

  useEffect(() => {
    if (phase !== 'accepted' || countdownPaused) return undefined

    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev === null || prev <= 1) {
          clearInterval(timer)
          reset()
          return null
        }
        return prev - 1
      })
    }, 1000)

    return () => clearInterval(timer)
  }, [phase, countdownPaused, reset])

  const pauseCountdown = useCallback(() => {
    setCountdownPaused(true)
    setCountdown(null)
  }, [])

  const reconcile = async (currentCode) => {
    setPhase('reconciling')
    try {
      const freshLookup = await poolAccessService.lookup(currentCode)
      const ticket = ticketOf(freshLookup)
      const ticketId = valueOf(ticket, 'id', 'Id')
      if (ticketId) {
        const history = await poolAccessService.getEntries(ticketId, vietnamDate())
        const entries = valueOf(history, 'items', 'Items') || []
        if (hasAcceptedEntry(entries)) {
          setResult(freshLookup)
          setPhase('accepted')
          setCountdown(3)
          setCountdownPaused(false)
          setNotice({ type: 'info', message: 'Đã ghi nhận lượt vào bể trong lịch sử hôm nay.' })
          return
        }
      }
      setResult(freshLookup)
      setPhase('denied')
      setNotice({ type: 'warning', message: resultMessage(freshLookup, 'Chưa thấy lượt vào được ghi nhận hoặc trạng thái vé không hợp lệ.') })
    } catch {
      setPhase('locked')
      setNotice({ type: 'warning', message: 'Không thể đối soát kết quả xác nhận. Vui lòng kiểm tra lại kết nối mạng.' })
    }
  }

  const scanAndConfirm = async (nextCode, nextInputMode) => {
    const preparedCode = prepareTicketCode(nextCode)
    if (!preparedCode.accepted) {
      setNotice({ type: 'error', message: preparedCode.reason })
      return
    }

    setPhase('processing')
    setNotice(null)

    try {
      const response = await poolAccessService.confirm(preparedCode.payload, nextInputMode)
      setResult(response)
      if (valueOf(response, 'allowed', 'Allowed') === true) {
        setPhase('accepted')
        setCountdown(3)
        setCountdownPaused(false)
        const t = ticketOf(response)
        const rem = valueOf(t, 'remainingEntries', 'RemainingEntries')
        const extraMsg = rem !== undefined && rem !== null && rem > 0
          ? ` (Còn lại ${rem} lượt - có thể quét tiếp cho người đi cùng)`
          : ''
        setNotice({ type: 'success', message: `${resultMessage(response, 'Vé hợp lệ. Đã xác nhận lượt vào bể.')}${extraMsg}` })
      } else {
        setPhase('denied')
        setNotice({ type: 'error', message: resultMessage(response, 'Vé không thể vào bể.') })
      }
    } catch (error) {
      if (shouldReconcileConfirmation(error)) {
        await reconcile(preparedCode.payload)
      } else {
        setResult(null)
        setPhase('idle')
        setNotice({ type: 'error', message: resultMessage(error?.response?.data, 'Không thể kết nối máy chủ để xác nhận vé. Vui lòng kiểm tra kết nối mạng và thử lại.') })
      }
    }
  }

  const currentTicket = ticketOf(result)
  const remainingEntries = valueOf(currentTicket, 'remainingEntries', 'RemainingEntries')
  const hasRemainingEntries = remainingEntries !== undefined && remainingEntries !== null && remainingEntries > 0

  return (
    <div className="service-list-page">
      <ServiceListHeader title="Quét QR vào cổng" />
      <section className="service-list-table" aria-label="Quét và xác nhận vé vào bể">
        <Card>
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Segmented
              aria-label="Chọn cách nhập mã vé"
              options={[{ label: 'Quét QR', value: INPUT_MODE.qr, icon: <QrcodeOutlined /> }, { label: 'Nhập mã', value: INPUT_MODE.manual, icon: <SearchOutlined /> }]}
              value={source}
              onChange={(value) => { reset(); setSource(value) }}
            />
            {source === INPUT_MODE.qr ? (
              <>
                <Button onClick={() => setScannerOpen((current) => !current)}>{scannerOpen ? 'Dừng quét' : 'Bắt đầu quét'}</Button>
                <QrScanner key={scannerKey} open={scannerOpen} autoStart expectedPurpose={QR_PURPOSE.ticket} onDecoded={(payload) => scanAndConfirm(payload, INPUT_MODE.qr)} onReset={reset} />
              </>
            ) : (
              <Space.Compact style={{ width: '100%' }}>
                <Input aria-label="Mã vé" maxLength={255} placeholder="Nhập mã vé" value={manualCode} onChange={(event) => setManualCode(event.target.value)} onPressEnter={() => scanAndConfirm(manualCode, INPUT_MODE.manual)} />
                <Button type="primary" onClick={() => scanAndConfirm(manualCode, INPUT_MODE.manual)} disabled={manualCode.length === 0} loading={phase === 'processing'}>Xác nhận vé</Button>
              </Space.Compact>
            )}
            {(phase === 'processing' || phase === 'reconciling') && <Spin description="Đang kiểm tra và xác nhận vé..." />}
            {notice && (
              <Alert
                showIcon
                type={notice.type}
                title={notice.message}
                description={countdown !== null ? `Tự động chuyển sang quét vé tiếp theo sau ${countdown} giây...` : undefined}
              />
            )}
            <TicketPreview result={result} />
            {phase === 'accepted' && (
              <Space wrap>
                {hasRemainingEntries && (
                  <Button
                    type="primary"
                    size="large"
                    style={{ backgroundColor: '#52c41a', borderColor: '#52c41a' }}
                    icon={<UsergroupAddOutlined />}
                    onClick={() => {
                      pauseCountdown()
                      if (source === INPUT_MODE.manual && manualCode) {
                        scanAndConfirm(manualCode, INPUT_MODE.manual)
                      } else {
                        setScannerKey((current) => current + 1)
                        setScannerOpen(true)
                        setNotice({
                          type: 'info',
                          message: `Đang chờ quét tiếp cho người đi cùng nhóm (Vé còn ${remainingEntries} lượt)`
                        })
                      }
                    }}
                  >
                    Quét tiếp cho người đi cùng (Còn {remainingEntries} lượt)
                  </Button>
                )}
                <Button type="primary" size="large" icon={<QrcodeOutlined />} onClick={reset}>
                  Quét vé tiếp theo {countdown !== null ? `(${countdown}s)` : ''}
                </Button>
                {countdown !== null && (
                  <Button onClick={pauseCountdown}>
                    Tạm dừng đếm ngược
                  </Button>
                )}
              </Space>
            )}
            {(phase === 'denied' || phase === 'locked') && (
              <Button type="primary" size="large" icon={<QrcodeOutlined />} onClick={reset}>
                Quét vé khác
              </Button>
            )}
          </Space>
        </Card>
      </section>
    </div>
  )
}
