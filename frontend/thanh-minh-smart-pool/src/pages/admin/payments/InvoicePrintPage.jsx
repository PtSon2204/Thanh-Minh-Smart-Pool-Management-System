import { useEffect } from 'react'
import { useParams } from 'react-router-dom'
import { Spin, Alert } from 'antd'
import dayjs from 'dayjs'
import { usePaymentDetail } from '../../../features/payments/hooks/usePayments'
import logoImg from '../../../assets/logo-be-boi-thanh-minh.png'
import './InvoicePrintPage.css'

const formatCurrency = (val) => {
  if (val === null || val === undefined) return '0 ₫'
  return new Intl.NumberFormat('vi-VN').format(val) + ' ₫'
}

const getStatusLabel = (status) => {
  if (status === 'COMPLETED') return { label: 'Đã thanh toán', cls: 'status-completed' }
  if (status === 'FAILED') return { label: 'Thất bại', cls: 'status-failed' }
  return { label: 'Chờ xử lý', cls: 'status-pending' }
}

const getMethodLabel = (method) => {
  if (method === 'BANK_TRANSFER' || method === 'TRANSFER') return 'Chuyển khoản ngân hàng'
  if (method === 'CASH') return 'Tiền mặt'
  return method || '---'
}

const getTicketCategoryLabel = (cat) => {
  if (cat === 'VE_THANG') return 'Vé tháng'
  if (cat === 'VE_LUOT') return 'Vé lượt'
  return cat || ''
}

const getTicketStatusLabel = (st) => {
  if (st === 'ACTIVE') return 'Còn hiệu lực'
  if (st === 'EXPIRED') return 'Đã hết hạn'
  if (st === 'USED') return 'Đã sử dụng'
  return st || '---'
}

export default function InvoicePrintPage() {
  const { id } = useParams()
  const { data, isLoading, error } = usePaymentDetail(id)

  // Auto trigger print once data is ready
  useEffect(() => {
    if (data) {
      const timer = setTimeout(() => {
        window.print()
      }, 600)
      return () => clearTimeout(timer)
    }
  }, [data])

  if (isLoading) {
    return (
      <div className="invoice-loading">
        <Spin size="large" tip="Đang tải dữ liệu hóa đơn..." />
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="invoice-loading">
        <Alert
          type="error"
          message="Không thể tải hóa đơn"
          description="Vui lòng kiểm tra lại mã hóa đơn hoặc thử lại sau."
          showIcon
        />
      </div>
    )
  }

  const statusInfo = getStatusLabel(data.status)
  const issueTime = dayjs().format('DD/MM/YYYY HH:mm:ss')
  const paymentTime = data.paymentTime
    ? dayjs(data.paymentTime).format('DD/MM/YYYY HH:mm:ss')
    : '---'

  return (
    <div className="invoice-wrapper">
      {/* ============ PRINT TOOLBAR (hidden on print) ============ */}
      <div className="invoice-toolbar no-print">
        <div className="invoice-toolbar-inner">
          <span className="invoice-toolbar-title">Xem trước hóa đơn – {data.invoiceCode}</span>
          <div className="invoice-toolbar-actions">
            <button className="invoice-btn invoice-btn-print" onClick={() => window.print()}>
              🖨️ In hóa đơn
            </button>
            <button className="invoice-btn invoice-btn-close" onClick={() => window.close()}>
              ✕ Đóng
            </button>
          </div>
        </div>
      </div>

      {/* ============ INVOICE DOCUMENT ============ */}
      <div className="invoice-page">
        {/* ---- HEADER ---- */}
        <div className="invoice-header">
          <div className="invoice-header-left">
            <div className="invoice-logo-block">
              <img src={logoImg} alt="Logo Bể Bơi Thanh Minh" className="invoice-logo-img" />
              <div>
                <div className="invoice-company-name">SMART POOL</div>
                <div className="invoice-company-tagline">Hệ thống quản lý bể bơi thông minh</div>
              </div>
            </div>
            <div className="invoice-company-info">
              <div>📍 Đông Anh, Hà Nội</div>
              <div>📞 1900 9999 | 📧 contact@smartpool.vn</div>
              <div>🌐 www.smartpool.vn</div>
            </div>
          </div>
          <div className="invoice-header-right">
            <div className="invoice-title-block">
              <div className="invoice-title">HÓA ĐƠN ĐIỆN TỬ</div>
              <div className="invoice-subtitle">ELECTRONIC INVOICE</div>
            </div>
            <div className="invoice-meta">
              <div className="invoice-meta-row">
                <span className="invoice-meta-label">Số HĐ:</span>
                <span className="invoice-meta-value invoice-code">{data.invoiceCode}</span>
              </div>
              <div className="invoice-meta-row">
                <span className="invoice-meta-label">Ngày xuất:</span>
                <span className="invoice-meta-value">{issueTime}</span>
              </div>
              <div className="invoice-meta-row">
                <span className="invoice-meta-label">Trạng thái:</span>
                <span className={`invoice-status-badge ${statusInfo.cls}`}>{statusInfo.label}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="invoice-divider" />

        {/* ---- CUSTOMER & PAYMENT INFO ---- */}
        <div className="invoice-info-grid">
          {/* Customer block */}
          <div className="invoice-info-block">
            <div className="invoice-info-block-title">👤 Thông tin khách hàng</div>
            <table className="invoice-info-table">
              <tbody>
                <tr>
                  <td className="info-label">Họ và tên:</td>
                  <td className="info-value">{data.customerName || 'Khách vãng lai'}</td>
                </tr>
                <tr>
                  <td className="info-label">Số điện thoại:</td>
                  <td className="info-value">{data.customerPhone || '---'}</td>
                </tr>
                <tr>
                  <td className="info-label">Email:</td>
                  <td className="info-value">{data.customerEmail || '---'}</td>
                </tr>
                <tr>
                  <td className="info-label">Địa chỉ:</td>
                  <td className="info-value">{data.customerAddress || '---'}</td>
                </tr>
              </tbody>
            </table>
          </div>

          {/* Payment block */}
          <div className="invoice-info-block">
            <div className="invoice-info-block-title">💳 Thông tin thanh toán</div>
            <table className="invoice-info-table">
              <tbody>
                <tr>
                  <td className="info-label">Mã đơn hàng:</td>
                  <td className="info-value">{data.orderCode || data.orderId}</td>
                </tr>
                <tr>
                  <td className="info-label">Thời gian TT:</td>
                  <td className="info-value">{paymentTime}</td>
                </tr>
                <tr>
                  <td className="info-label">Phương thức:</td>
                  <td className="info-value">{getMethodLabel(data.paymentMethod)}</td>
                </tr>
                {data.transactionRef && (
                  <tr>
                    <td className="info-label">Mã tham chiếu:</td>
                    <td className="info-value invoice-ref">{data.transactionRef}</td>
                  </tr>
                )}
                {data.staffName && (
                  <tr>
                    <td className="info-label">Nhân viên thu:</td>
                    <td className="info-value">{data.staffName}</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        <div className="invoice-section-title">📋 Chi tiết hàng hóa / Dịch vụ</div>

        {/* ---- ITEMS TABLE ---- */}
        <table className="invoice-items-table">
          <thead>
            <tr>
              <th className="col-no">STT</th>
              <th className="col-name">Tên vé / Dịch vụ</th>
              <th className="col-category">Phân loại</th>
              <th className="col-qty">SL</th>
              <th className="col-price">Đơn giá</th>
              <th className="col-total">Thành tiền</th>
            </tr>
          </thead>
          <tbody>
            {(data.items || []).map((item, idx) => (
              <tr key={item.id || idx} className={idx % 2 === 0 ? 'row-even' : 'row-odd'}>
                <td className="col-no">{idx + 1}</td>
                <td className="col-name">{item.itemName}</td>
                <td className="col-category">{getTicketCategoryLabel(item.ticketCategory)}</td>
                <td className="col-qty">{item.quantity}</td>
                <td className="col-price">{formatCurrency(item.unitPrice)}</td>
                <td className="col-total">{formatCurrency(item.quantity * item.unitPrice)}</td>
              </tr>
            ))}
            {(!data.items || data.items.length === 0) && (
              <tr>
                <td colSpan={6} className="no-items">Không có dữ liệu</td>
              </tr>
            )}
          </tbody>
        </table>

        {/* ---- TOTALS ---- */}
        <div className="invoice-totals">
          <table className="invoice-totals-table">
            <tbody>
              <tr>
                <td className="totals-label">Tổng tiền hàng:</td>
                <td className="totals-value">{formatCurrency(data.totalAmount)}</td>
              </tr>
              {data.discountAmount > 0 && (
                <tr className="totals-discount">
                  <td className="totals-label">
                    Giảm giá voucher{data.voucherCode ? ` (${data.voucherCode})` : ''}:
                  </td>
                  <td className="totals-value">- {formatCurrency(data.discountAmount)}</td>
                </tr>
              )}
              <tr className="totals-final">
                <td className="totals-label">TỔNG THANH TOÁN:</td>
                <td className="totals-value totals-final-value">{formatCurrency(data.amount)}</td>
              </tr>
              <tr>
                <td className="totals-label">Bằng chữ:</td>
                <td className="totals-value totals-words">
                  {numberToVietnameseWords(data.amount)} đồng chẵn
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* ---- TICKETS QR INFO ---- */}
        {data.tickets && data.tickets.length > 0 && (
          <>
            <div className="invoice-section-title">🎫 Thông tin vé điện tử đã cấp phát</div>
            <table className="invoice-items-table invoice-tickets-table">
              <thead>
                <tr>
                  <th className="col-no">STT</th>
                  <th>Loại vé</th>
                  <th>Mã QR</th>
                  <th>Ngày phát hành</th>
                  <th>Ngày hết hạn</th>
                  <th>Lượt còn lại</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {data.tickets.map((ticket, idx) => (
                  <tr key={ticket.ticketId || idx} className={idx % 2 === 0 ? 'row-even' : 'row-odd'}>
                    <td className="col-no">{idx + 1}</td>
                    <td>{ticket.ticketName}</td>
                    <td className="ticket-qr-code">{ticket.qrCode}</td>
                    <td>
                      {ticket.issueDate ? dayjs(ticket.issueDate).format('DD/MM/YYYY') : '---'}
                    </td>
                    <td>
                      {ticket.expiryDate ? dayjs(ticket.expiryDate).format('DD/MM/YYYY') : '---'}
                    </td>
                    <td className="col-qty">
                      {ticket.remainingEntries !== null && ticket.remainingEntries !== undefined
                        ? ticket.remainingEntries
                        : 'Không giới hạn'}
                    </td>
                    <td>
                      <span className={`ticket-status ticket-status-${(ticket.status || '').toLowerCase()}`}>
                        {getTicketStatusLabel(ticket.status)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}

        {/* ---- FOOTER ---- */}
        <div className="invoice-footer">
          <div className="invoice-footer-signatures">
            <div className="invoice-signature">
              <div className="signature-title">Khách hàng</div>
              <div className="signature-space" />
              <div className="signature-name">{data.customerName || 'Khách vãng lai'}</div>
            </div>
            <div className="invoice-signature">
              <div className="signature-title">Nhân viên thu ngân</div>
              <div className="signature-space" />
              <div className="signature-name">{data.staffName || 'Hệ thống tự động'}</div>
            </div>
          </div>
          <div className="invoice-footer-legal">
            <span>Smart Pool Management System © {new Date().getFullYear()}</span>
            <span>Hóa đơn được tạo lúc: {issueTime}</span>
          </div>
        </div>
      </div>
    </div>
  )
}

// ---- Helper: convert number to Vietnamese words (simplified) ----
function numberToVietnameseWords(num) {
  if (!num || isNaN(num)) return 'Không'
  const ones = ['', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín']
  const teens = [
    'mười', 'mười một', 'mười hai', 'mười ba', 'mười bốn', 'mười lăm',
    'mười sáu', 'mười bảy', 'mười tám', 'mười chín',
  ]
  const tens = [
    '', '', 'hai mươi', 'ba mươi', 'bốn mươi', 'năm mươi',
    'sáu mươi', 'bảy mươi', 'tám mươi', 'chín mươi',
  ]

  const toWords = (n) => {
    if (n === 0) return ''
    if (n < 10) return ones[n]
    if (n < 20) return teens[n - 10]
    if (n < 100) {
      const t = Math.floor(n / 10)
      const o = n % 10
      return tens[t] + (o ? ' ' + (o === 5 ? 'lăm' : ones[o]) : '')
    }
    if (n < 1000) {
      const h = Math.floor(n / 100)
      const rem = n % 100
      return ones[h] + ' trăm' + (rem ? ' ' + toWords(rem) : '')
    }
    if (n < 1_000_000) {
      const th = Math.floor(n / 1000)
      const rem = n % 1000
      return toWords(th) + ' nghìn' + (rem ? ' ' + toWords(rem) : '')
    }
    if (n < 1_000_000_000) {
      const m = Math.floor(n / 1_000_000)
      const rem = n % 1_000_000
      return toWords(m) + ' triệu' + (rem ? ' ' + toWords(rem) : '')
    }
    const b = Math.floor(n / 1_000_000_000)
    const rem = n % 1_000_000_000
    return toWords(b) + ' tỷ' + (rem ? ' ' + toWords(rem) : '')
  }

  const rounded = Math.round(num)
  const words = toWords(rounded)
  return words.charAt(0).toUpperCase() + words.slice(1)
}
