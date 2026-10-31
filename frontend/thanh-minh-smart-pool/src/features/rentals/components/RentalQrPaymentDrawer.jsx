import { Alert, Button, Descriptions, Drawer, Popconfirm, Result, Spin, Tag } from 'antd'
import { CheckCircleOutlined, ClockCircleOutlined } from '@ant-design/icons'
import RentalBankTransferQr from './RentalBankTransferQr'
import { getRentalErrorMessage } from '../hooks/useRentalMutations'
import { isRentalPaymentWaiting, requiresRentalPaymentReconciliation } from '../state/rentalPaymentState'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function formatAmount(value) {
  return currencyFormatter.format(value || 0)
}

function formatDeadline(seconds) {
  return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
}

function PaymentSummary({ payment }) {
  return <Descriptions column={1} bordered size="small" title="Thông tin thu tiền">
    <Descriptions.Item label="Mã yêu cầu"><span className="rental-reference-value">{payment.transactionRef}</span></Descriptions.Item>
    <Descriptions.Item label="Khách hàng">{payment.customerName} {payment.customerPhone ? `· ${payment.customerPhone}` : ''}</Descriptions.Item>
    <Descriptions.Item label="Phí thuê">{formatAmount(payment.feeAmount)}</Descriptions.Item>
    <Descriptions.Item label="Tiền cọc">{formatAmount(payment.depositAmount)}</Descriptions.Item>
    <Descriptions.Item label="Tổng cần thu">{formatAmount(payment.totalToCollect)}</Descriptions.Item>
    <Descriptions.Item label="Đã nhận">{formatAmount(payment.paidAmount)}</Descriptions.Item>
    <Descriptions.Item label="Còn thiếu">{formatAmount(payment.remainingAmount)}</Descriptions.Item>
    {Number(payment.excessAmount) > 0 && <Descriptions.Item label="Chuyển dư">{formatAmount(payment.excessAmount)}</Descriptions.Item>}
  </Descriptions>
}

export default function RentalQrPaymentDrawer({ paymentState }) {
  const { payment, open, loading, error, refresh, refreshing, secondsLeft, close, finish, issue, issuing, issueError, cancel, cancelling, cancelError, retryCreate, creating, restoredCreateUncertain, hasUncertainOutcome } = paymentState
  const waiting = isRentalPaymentWaiting(payment?.status)
  const reconciliationRequired = requiresRentalPaymentReconciliation(payment)
  const closeDrawer = () => {
    if (!hasUncertainOutcome && !issuing && !cancelling) close()
  }

  return <Drawer rootClassName="rental-payment-drawer" title="Thu tiền QR cho đơn thuê" open={open} onClose={closeDrawer}
    closable={!hasUncertainOutcome && !issuing && !cancelling} mask={{ closable: !hasUncertainOutcome && !issuing && !cancelling }} size="large">
    {loading && !payment && <div className="admin-management-state"><Spin size="large" /></div>}
    {error && !payment && <Result status="warning" title="Không thể tải trạng thái thanh toán" subTitle={getRentalErrorMessage(error, 'Thời hạn của yêu cầu vẫn do máy chủ quản lý.')} extra={<><Button loading={refreshing} onClick={() => refresh()}>Kiểm tra lại</Button>{restoredCreateUncertain && <Button loading={creating} onClick={() => { void retryCreate().catch(() => {}) }}>Thử lại cùng yêu cầu</Button>}</>} />}
    {!loading && !error && !payment && <Result status="warning" title="Không tìm thấy yêu cầu thanh toán" extra={<Button onClick={finish}>Đóng</Button>} />}
    {payment && <div className="rental-payment-stack">
      <div className="rental-payment-status"><Tag color={payment.status === 'PAID' || payment.status === 'ISSUED' ? 'success' : reconciliationRequired ? 'warning' : 'processing'}>{payment.status}</Tag>{reconciliationRequired && <span>Yêu cầu đối soát thủ công</span>}</div>
      <PaymentSummary payment={payment} />
      {payment.items?.length > 0 && <section className="rental-quote-preview" aria-label="Sản phẩm trong yêu cầu thanh toán">
        {payment.items.map((item) => <div className="rental-payment-item" key={item.productId}><strong>{item.name}</strong><span>{item.quantity} × {formatAmount(item.unitPrice)}</span></div>)}
      </section>}
      {hasUncertainOutcome && <Alert type="warning" showIcon message="Kết quả yêu cầu trước chưa xác định" description="Không đổi phương thức hoặc tạo yêu cầu mới. Hãy kiểm tra lại trạng thái của cùng yêu cầu này." action={<Button size="small" loading={refreshing} onClick={() => refresh()}>Kiểm tra lại</Button>} />}
      {issueError && <Alert type="error" showIcon message="Không thể xuất đồ" description={getRentalErrorMessage(issueError, 'Hãy kiểm tra lại cùng yêu cầu trước khi thử lại.')} />}
      {cancelError && <Alert type="error" showIcon message="Không thể hủy yêu cầu QR" description={getRentalErrorMessage(cancelError, 'Hãy kiểm tra lại trạng thái trước khi thao tác tiếp.')} />}
      {payment.status === 'ISSUED' ? <Result status="success" icon={<CheckCircleOutlined />} title="Đã xuất đồ thành công" subTitle="Đơn thuê đã được tạo từ yêu cầu QR này." extra={<Button type="primary" onClick={finish}>Hoàn tất</Button>} />
        : payment.status === 'PAID' ? <Result status="success" title="Đã nhận đủ tiền" subTitle={`Đã nhận ${formatAmount(payment.paidAmount)}. Nhân viên cần xác nhận xuất đồ.`}
          extra={<Button type="primary" loading={issuing} disabled={hasUncertainOutcome || issuing} onClick={() => { void issue().catch(() => {}) }}>Xác nhận xuất đồ</Button>} />
          : waiting && secondsLeft > 0 ? <>
            {payment.status === 'PARTIAL' && <Alert type="warning" showIcon message="Đã nhận một phần thanh toán" description="Mã QR dùng số tiền còn thiếu do máy chủ trả về." />}
            <h3 className="rental-payment-heading">Quét mã QR để thanh toán</h3>
            <RentalBankTransferQr bankId={payment.bankId} accountNumber={payment.accountNumber} accountName={payment.accountName}
              amount={payment.remainingAmount} transactionRef={payment.transactionRef} downloadName={`rental-qr-${payment.id}.png`}
              alt="Mã QR thanh toán đơn thuê" showBankDetails />
            <div className="online-payment-waiting"><Spin size="small" /><span>Đang chờ máy chủ xác nhận thanh toán...</span></div>
            <div className={`online-payment-countdown${secondsLeft <= 60 ? ' is-urgent' : ''}`}><span className="online-payment-clock-icon"><ClockCircleOutlined /></span><div><span>HẾT HẠN SAU</span><strong role="timer">{formatDeadline(secondsLeft)}</strong></div></div>
            <p className="online-payment-deadline-note">Máy chủ tự quản lý thời hạn năm phút. Đóng màn hình không hủy yêu cầu.</p>
            <Popconfirm title="Hủy yêu cầu QR này?" description="Thao tác này chỉ hủy yêu cầu QR, không xuất đồ." okText="Hủy yêu cầu" cancelText="Giữ lại" onConfirm={() => { void cancel().catch(() => {}) }}>
              <Button danger disabled={hasUncertainOutcome || cancelling} loading={cancelling}>Hủy yêu cầu QR</Button>
            </Popconfirm>
          </> : waiting ? <div className="online-payment-verifying"><Spin /><strong>Đang xác nhận trạng thái yêu cầu QR...</strong><Button loading={refreshing} onClick={() => refresh()}>Kiểm tra lại</Button></div>
            : <Result status="warning" title={payment.status === 'EXPIRED' ? 'Yêu cầu QR đã hết hạn' : 'Yêu cầu QR đã hủy'}
            subTitle={reconciliationRequired ? 'Khoản tiền đã nhận, chuyển dư, hoặc thanh toán muộn cần được đối soát. Không tạo mã QR mới từ yêu cầu này.' : 'Yêu cầu này không còn có mã QR để thanh toán.'}
            extra={<Button onClick={finish}>Đóng</Button>} />}
    </div>}
  </Drawer>
}
