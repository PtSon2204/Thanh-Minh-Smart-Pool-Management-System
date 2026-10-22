import { useState } from 'react'
import { CheckCircleOutlined, QrcodeOutlined, SearchOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Descriptions, Input, Segmented, Space, Spin, Tag } from 'antd'
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

function isLookupValid(result) {
  return valueOf(result, 'found', 'Found') === true && valueOf(result, 'isValid', 'IsValid') === true
}

function ticketOf(result) {
  return valueOf(result, 'ticket', 'Ticket')
}

function TicketPreview({ lookup }) {
  const ticket = ticketOf(lookup)
  if (!ticket) return null

  return <Descriptions bordered column={1} size="small" title="Thông tin vé">
    <Descriptions.Item label="Loại vé">{valueOf(ticket, 'ticketTypeName', 'TicketTypeName') || 'Không xác định'}</Descriptions.Item>
    <Descriptions.Item label="Phân loại">{valueOf(ticket, 'category', 'Category') || 'Không xác định'}</Descriptions.Item>
    <Descriptions.Item label="Trạng thái">{valueOf(ticket, 'status', 'Status') || 'Không xác định'}</Descriptions.Item>
    <Descriptions.Item label="Hết hạn">{valueOf(ticket, 'expiryDate', 'ExpiryDate') ? new Date(valueOf(ticket, 'expiryDate', 'ExpiryDate')).toLocaleString('vi-VN') : 'Không áp dụng'}</Descriptions.Item>
    <Descriptions.Item label="Lượt còn lại">{valueOf(ticket, 'remainingEntries', 'RemainingEntries') ?? 'Không áp dụng'}</Descriptions.Item>
  </Descriptions>
}

export default function TicketScannerPage() {
  const [source, setSource] = useState(INPUT_MODE.qr)
  const [manualCode, setManualCode] = useState('')
  const [lookup, setLookup] = useState(null)
  const [code, setCode] = useState(null)
  const [inputMode, setInputMode] = useState(null)
  const [phase, setPhase] = useState('idle')
  const [notice, setNotice] = useState(null)
  const [scannerKey, setScannerKey] = useState(0)
  const [scannerOpen, setScannerOpen] = useState(true)

  const reset = () => {
    setScannerOpen(true)
    setScannerKey((current) => current + 1)
    setLookup(null)
    setCode(null)
    setInputMode(null)
    setPhase('idle')
    setNotice(null)
  }

  const lookupCode = async (nextCode, nextInputMode) => {
    const preparedCode = prepareTicketCode(nextCode)
    if (!preparedCode.accepted) {
      setNotice({ type: 'error', message: preparedCode.reason })
      return
    }

    setPhase('lookup')
    setNotice(null)
    setCode(preparedCode.payload)
    setInputMode(nextInputMode)
    try {
      const result = await poolAccessService.lookup(preparedCode.payload)
      setLookup(result)
      setPhase('preview')
    } catch (error) {
      setLookup(null)
      setPhase('idle')
      setNotice({ type: 'error', message: resultMessage(error?.response?.data, 'Không thể tra cứu vé. Hãy kiểm tra kết nối rồi quét hoặc nhập lại.') })
    }
  }

  const reconcile = async (currentCode, currentLookup) => {
    const ticket = ticketOf(currentLookup)
    const ticketId = valueOf(ticket, 'id', 'Id')
    if (!ticketId) {
      setPhase('locked')
      setNotice({ type: 'warning', message: 'Không thể đối soát vì phản hồi tra cứu không có mã định danh vé. Không gửi lại xác nhận tự động.' })
      return
    }

    setPhase('reconciling')
    try {
      const [history, freshLookup] = await Promise.all([
        poolAccessService.getEntries(ticketId, vietnamDate()),
        poolAccessService.lookup(currentCode),
      ])
      const entries = valueOf(history, 'items', 'Items') || []
      if (hasAcceptedEntry(entries)) {
        setLookup(freshLookup)
        setPhase('already-recorded')
        setNotice({ type: 'info', message: 'Đã ghi nhận lượt vào bể trong lịch sử hôm nay. Hệ thống không gán kết quả này cho lần xác nhận vừa mất phản hồi.' })
        return
      }
      setLookup(freshLookup)
      if (isLookupValid(freshLookup)) {
        setPhase('preview')
        setNotice({ type: 'warning', message: 'Chưa thấy lượt vào được ghi nhận. Vé vẫn hợp lệ. Hãy bấm Xác nhận vào bể một lần nữa nếu bạn muốn gửi yêu cầu mới.' })
        return
      }
      setPhase('locked')
      setNotice({ type: 'warning', message: `Không thể xác nhận lại: ${resultMessage(freshLookup, 'Trạng thái vé đã thay đổi.')}` })
    } catch {
      setPhase('locked')
      setNotice({ type: 'warning', message: 'Không thể đối soát kết quả xác nhận. Hệ thống giữ khóa để tránh ghi nhận trùng. Kiểm tra lịch sử vé trước khi thực hiện thao tác khác.' })
    }
  }

  const confirmEntry = async () => {
    if (!code || !lookup || !isLookupValid(lookup) || phase !== 'preview') return

    setPhase('confirming')
    setNotice(null)
    try {
      const result = await poolAccessService.confirm(code, inputMode)
      if (valueOf(result, 'allowed', 'Allowed') === true) {
        setPhase('accepted')
        setNotice({ type: 'success', message: resultMessage(result, 'Đã xác nhận lượt vào bể.') })
        return
      }
      setPhase('preview')
      setNotice({ type: 'error', message: resultMessage(result, 'Không thể xác nhận lượt vào bể.') })
    } catch (error) {
      if (shouldReconcileConfirmation(error)) await reconcile(code, lookup)
    }
  }

  const canConfirm = phase === 'preview' && isLookupValid(lookup)
  const previewType = isLookupValid(lookup) ? 'success' : 'error'

  return <div className="service-list-page">
    <ServiceListHeader title="Quét QR vào cổng" action={<Button onClick={reset}>Quét vé khác</Button>} />
    <section className="service-list-table" aria-label="Quét và xác nhận vé vào bể">
      <Card>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Segmented
            aria-label="Chọn cách nhập mã vé"
            options={[{ label: 'Quét QR', value: INPUT_MODE.qr, icon: <QrcodeOutlined /> }, { label: 'Nhập mã', value: INPUT_MODE.manual, icon: <SearchOutlined /> }]}
            value={source}
            onChange={(value) => { reset(); setSource(value) }}
          />
          {source === INPUT_MODE.qr ? <>
            <Button onClick={() => setScannerOpen((current) => !current)}>{scannerOpen ? 'Dừng quét' : 'Bắt đầu quét'}</Button>
            <QrScanner key={scannerKey} open={scannerOpen} expectedPurpose={QR_PURPOSE.ticket} onDecoded={(payload) => lookupCode(payload, INPUT_MODE.qr)} />
          </> : <Space.Compact style={{ width: '100%' }}>
            <Input aria-label="Mã vé" maxLength={255} placeholder="Nhập mã vé" value={manualCode} onChange={(event) => setManualCode(event.target.value)} onPressEnter={() => lookupCode(manualCode, INPUT_MODE.manual)} />
            <Button type="primary" onClick={() => lookupCode(manualCode, INPUT_MODE.manual)} disabled={manualCode.length === 0}>Tra cứu vé</Button>
          </Space.Compact>}
          {phase === 'lookup' && <Spin description="Đang tra cứu vé" />}
          {lookup && <Alert showIcon type={previewType} title={isLookupValid(lookup) ? 'Vé có thể vào bể' : 'Vé không thể vào bể'} description={resultMessage(lookup, 'Không thể xác định trạng thái vé.')} />}
          <TicketPreview lookup={lookup} />
          {notice && <Alert showIcon type={notice.type} title={notice.message} />}
          {lookup && <Button type="primary" icon={<CheckCircleOutlined />} onClick={confirmEntry} loading={phase === 'confirming' || phase === 'reconciling'} disabled={!canConfirm}>Xác nhận vào bể</Button>}
          {lookup && !isLookupValid(lookup) && <Tag color="error">Không thể xác nhận vé không hợp lệ hoặc không tìm thấy.</Tag>}
        </Space>
      </Card>
    </section>
  </div>
}
