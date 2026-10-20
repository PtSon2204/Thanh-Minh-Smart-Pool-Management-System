import { useState } from 'react'
import { Alert, Button, Modal, Result, Spin, message } from 'antd'
import { ClockCircleOutlined, DownloadOutlined, ShoppingOutlined, CheckCircleOutlined } from '@ant-design/icons'
import './OnlineTicketPaymentModal.css'

const currency = (value) => `${new Intl.NumberFormat('vi-VN').format(value ?? 0)} đ`

function QrImage({ url }) {
  const [loading, setLoading] = useState(true)
  const [failed, setFailed] = useState(false)
  return (
    <div className="online-payment-qr-image">
      {loading && <Spin size="large" />}
      {failed ? <Alert type="error" title="Không tải được mã QR. Vui lòng thử lại." /> : (
        <img src={url} alt="Mã QR thanh toán đơn vé" width="280" height="280"
          style={{ display: loading ? 'none' : 'block' }}
          onLoad={() => setLoading(false)} onError={() => { setLoading(false); setFailed(true) }} />
      )}
    </div>
  )
}

export default function OnlineTicketPaymentModal({ payment, onClose, onFinish, onReturnToCart }) {
  const { open, order, loading, error, secondsLeft, refresh, refreshing } = payment
  const [downloading, setDownloading] = useState(false)
  const completed = order?.status === 'COMPLETED'
  const cancelled = order?.status === 'CANCELLED'
  const waiting = order?.status === 'PENDING' || order?.status === 'PARTIAL'
  const canPay = waiting && secondsLeft > 0 && !error
  const bank = import.meta.env.VITE_VIETQR_BANK_ID || 'MB'
  const account = import.meta.env.VITE_VIETQR_ACCOUNT_NO || '03597040604'
  const parameters = new URLSearchParams({ amount: String(order?.remainingAmount ?? 0), addInfo: order?.transactionRef ?? '' })
  const qrUrl = canPay && order?.transactionRef
    ? `https://img.vietqr.io/image/${encodeURIComponent(bank)}-${encodeURIComponent(account)}-qr_only.png?${parameters}`
    : ''
  const countdown = `${String(Math.floor(secondsLeft / 60)).padStart(2, '0')}:${String(secondsLeft % 60).padStart(2, '0')}`

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
      link.download = `smartpool-qr-${order.orderId.slice(0, 8)}.png`
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch {
      message.error('Không thể tải mã QR. Vui lòng thử lại.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <Modal open={open} onCancel={onClose} footer={null} title={null} centered
      width={1080} className="online-ticket-payment-modal" destroyOnHidden
      styles={{ body: { padding: 0 } }}>
      {loading && !order ? (
        <div className="online-payment-loading"><Spin size="large" /><p>Đang tải thông tin đơn hàng...</p></div>
      ) : !order ? (
        <Result status="warning" title="Chưa thể tải thông tin thanh toán"
          subTitle="Vui lòng kiểm tra kết nối và thử lại."
          extra={<><Button loading={refreshing} onClick={() => refresh()}>Thử lại</Button><Button onClick={onReturnToCart}>Quay lại giỏ hàng</Button></>} />
      ) : (
        <div className="online-payment-layout">
          <section className="online-payment-order" aria-label="Thông tin đơn hàng">
            <span className="online-payment-eyebrow"><span /> XÁC NHẬN THANH TOÁN</span>
            <h2>Thông tin<br className="online-payment-title-break" /> đơn hàng</h2>
            <div className="online-payment-order-code"><ShoppingOutlined /><span>Đơn hàng <strong>#{order.orderId.slice(0, 8).toUpperCase()}</strong></span></div>
            <div className="online-payment-items">
              {order.items?.map((item) => (
                <div className="online-payment-item" key={item.ticketTypeId}>
                  <div><strong>{item.name}</strong><span>{item.quantity} × {currency(item.unitPrice)}</span></div>
                  <strong>{currency(item.lineTotal)}</strong>
                </div>
              ))}
            </div>
            <div className="online-payment-summary">
              <div><span>Tạm tính</span><strong>{currency(order.subtotal)}</strong></div>
              {order.discountAmount > 0 && <div className="online-payment-discount"><span>Giảm giá đã áp dụng</span><strong>− {currency(order.discountAmount)}</strong></div>}
              <div className="online-payment-total"><span>Tổng thanh toán</span><strong>{currency(order.finalAmount)}</strong></div>
              {order.paidAmount > 0 && <div><span>Đã nhận</span><strong>{currency(order.paidAmount)}</strong></div>}
              {order.status === 'PARTIAL' && <div className="online-payment-remaining"><span>Còn thiếu</span><strong>{currency(order.remainingAmount)}</strong></div>}
            </div>
            {order.status === 'PARTIAL' && !cancelled && <Alert type="warning" showIcon title="Đã nhận một phần thanh toán"
              description="Mã QR đã cập nhật phần còn thiếu. Vui lòng hoàn tất trong thời gian còn lại." />}
            <p className="online-payment-order-note">Thông tin và giá tiền được xác nhận theo đơn hàng đã tạo.</p>
          </section>
          <section className="online-payment-transfer" aria-label="Thanh toán bằng mã QR">
            {completed ? (
              <Result status="success" icon={<CheckCircleOutlined />} title="Thanh toán thành công!"
                subTitle="Vé đã được phát hành và lưu vào thẻ của bạn."
                extra={<Button type="primary" size="large" onClick={onFinish}>Xem thẻ của tôi</Button>} />
            ) : cancelled ? (
              <Result status="warning" title="Đơn hàng đã hết hạn"
                subTitle={order.paidAmount > 0
                  ? 'Đơn chưa được thanh toán đủ trong 5 phút nên đã tự hủy. Khoản tiền đã nhận được giữ lại để đối soát.'
                  : 'Đơn chưa được thanh toán trong 5 phút nên đã tự hủy. Vui lòng tạo đơn mới để tiếp tục.'}
                extra={<Button type="primary" size="large" onClick={onReturnToCart}>Quay lại giỏ hàng</Button>} />
            ) : (
              <>
                <h3>Quét mã QR để thanh toán</h3>
                {error ? <Result status="warning" title="Chưa thể xác minh trạng thái"
                  subTitle="Vui lòng kiểm tra kết nối. Thời hạn đơn hàng vẫn được giữ nguyên."
                  extra={<Button loading={refreshing} onClick={() => refresh()}>Kiểm tra lại</Button>} /> : secondsLeft === 0 ? (
                  <div className="online-payment-verifying"><Spin /><strong>Đang xác nhận trạng thái đơn hàng...</strong></div>
                ) : (
                  <>
                    <div className="online-payment-qr-card">
                      <QrImage key={qrUrl} url={qrUrl} />
                      <Button className="online-payment-download" icon={<DownloadOutlined />} loading={downloading}
                        onClick={downloadQr}>Tải mã QR</Button>
                    </div>
                    <div className="online-payment-waiting"><Spin size="small" /><span>Đang chờ thanh toán...</span></div>
                  </>
                )}
                <div className={`online-payment-countdown${secondsLeft <= 60 ? ' is-urgent' : ''}`}>
                  <span className="online-payment-clock-icon"><ClockCircleOutlined /></span>
                  <div><span>HẾT HẠN SAU</span><strong role="timer" aria-label="Thời gian thanh toán còn lại">{countdown}</strong></div>
                </div>
                <p className="online-payment-deadline-note">Đơn hàng tự hủy nếu chưa thanh toán đủ trong 5 phút.</p>
              </>
            )}
          </section>
        </div>
      )}
    </Modal>
  )
}
