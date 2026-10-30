import { useState } from 'react'
import { Alert, Button, Spin, message } from 'antd'
import { DownloadOutlined } from '@ant-design/icons'
import '../../payments/components/OnlineTicketPaymentModal.css'
import '../styles/rentalBankTransferQr.css'

export function getBankTransferQrUrl({ bankId, accountNumber, accountName, amount, transactionRef }) {
  if (!bankId || !accountNumber || !transactionRef || !Number.isFinite(Number(amount)) || Number(amount) <= 0) return ''
  const parameters = new URLSearchParams({ amount: String(amount), addInfo: transactionRef })
  if (accountName) parameters.set('accountName', accountName)
  return `https://img.vietqr.io/image/${encodeURIComponent(bankId)}-${encodeURIComponent(accountNumber)}-qr_only.png?${parameters}`
}

function QrImage({ url, alt }) {
  const [loading, setLoading] = useState(true)
  const [failed, setFailed] = useState(false)
  return <div className="online-payment-qr-image">
    {loading && <Spin size="large" />}
    {failed ? <Alert type="error" title="Không tải được mã QR. Vui lòng thử lại." /> : <img src={url} alt={alt} width="280" height="280"
      style={{ display: loading ? 'none' : 'block' }}
      onLoad={() => setLoading(false)} onError={() => { setLoading(false); setFailed(true) }} />}
  </div>
}

export default function RentalBankTransferQr({ bankId, accountNumber, accountName, amount, transactionRef, downloadName, alt, showBankDetails = false }) {
  const [downloading, setDownloading] = useState(false)
  const qrUrl = getBankTransferQrUrl({ bankId, accountNumber, accountName, amount, transactionRef })

  const downloadQr = async () => {
    if (!qrUrl || downloading) return
    setDownloading(true)
    try {
      const response = await fetch(qrUrl)
      if (!response.ok) throw new Error('QR download failed')
      const blob = await response.blob()
      if (!blob.type.startsWith('image/')) throw new Error('Invalid QR image')
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = downloadName || 'smartpool-qr.png'
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch {
      message.error('Không thể tải mã QR. Vui lòng thử lại.')
    } finally {
      setDownloading(false)
    }
  }

  if (!qrUrl) return <Alert type="warning" showIcon message="Không có thông tin QR hợp lệ" description="Máy chủ chưa trả về đủ thông tin ngân hàng hoặc số tiền cần thu." />

  return <>
    <div className="online-payment-qr-card">
      <QrImage key={qrUrl} url={qrUrl} alt={alt || 'Mã QR thanh toán'} />
      <Button className="online-payment-download" icon={<DownloadOutlined />} loading={downloading} onClick={downloadQr}>Tải mã QR</Button>
    </div>
    {showBankDetails && <dl className="bank-transfer-details">
      <div><dt>Ngân hàng</dt><dd>{bankId}</dd></div>
      <div><dt>Số tài khoản</dt><dd>{accountNumber}</dd></div>
      <div><dt>Chủ tài khoản</dt><dd>{accountName || '-'}</dd></div>
      <div><dt>Nội dung chuyển khoản</dt><dd>{transactionRef}</dd></div>
    </dl>}
  </>
}
