import { Alert, Button, Descriptions, Drawer, Empty, Spin, Tag, Typography } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
}

function formatAmount(value) {
  return value == null ? '-' : currencyFormatter.format(value)
}

function getDetailsError(error) {
  const status = error?.response?.status
  if (status === 404) return 'Nhóm đơn thuê này không còn tồn tại.'
  if (status === 401 || status === 403) return 'Bạn không có quyền xem chi tiết đơn thuê này.'
  return error?.response?.data?.message || 'Không thể tải chi tiết đơn thuê. Hãy thử lại.'
}

function formatOrderStatus(value) {
  if (value === 'Renting') return 'Đang thuê'
  if (value === 'Completed') return 'Đã hoàn tất'
  return value || '-'
}

function rentalStatus(record) {
  const states = {
    Renting: ['processing', 'Đang thuê'],
    Returned: ['success', 'Trả bình thường'],
    Damaged: ['warning', 'Hư hỏng'],
    Lost: ['error', 'Thất lạc'],
  }
  return states[record.status] || ['default', record.status || '-']
}

function TransactionHistory({ transactions }) {
  return <>
    <Typography.Title level={5}>Lịch sử thu và quyết toán</Typography.Title>
    {!transactions?.length && <Empty description="Chưa có lịch sử thu hoặc quyết toán." />}
    <div className="rental-history-list">
      {(transactions || []).map((transaction) => {
        const isCollection = transaction.kind === 'Collection'
        return <article className="rental-history-record" key={transaction.id}>
          <div className="rental-history-record-heading"><strong>{isCollection ? 'Thu tiền toàn đơn' : 'Quyết toán nhóm sản phẩm'}</strong><Tag color={isCollection ? 'processing' : 'success'}>{isCollection ? 'Collection' : 'Settlement'}</Tag></div>
          {isCollection && <p className="rental-history-note">Số tiền thu là tổng của toàn đơn, không phải riêng nhóm sản phẩm này.</p>}
          {!isCollection && transaction.productId && <p className="rental-history-note">Quyết toán cho nhóm sản phẩm: <span className="rental-reference-value">{transaction.productId}</span></p>}
          <dl>
            <div><dt>Phí thuê</dt><dd>{formatAmount(transaction.feeAmount)}</dd></div>
            <div><dt>Cọc</dt><dd>{formatAmount(transaction.depositAmount)}</dd></div>
            <div><dt>Đã hoàn</dt><dd>{formatAmount(transaction.refundAmount)}</dd></div>
            <div><dt>Đã giữ</dt><dd>{formatAmount(transaction.retainedAmount)}</dd></div>
            <div><dt>Xác nhận lúc</dt><dd>{formatDate(transaction.confirmedAtUtc)}</dd></div>
            <div><dt>Nhân viên xác nhận</dt><dd><span className="rental-reference-value">{transaction.confirmedByUserId || '-'}</span></dd></div>
            {transaction.reason && <div><dt>Lý do</dt><dd>{transaction.reason}</dd></div>}
          </dl>
        </article>
      })}
    </div>
  </>
}

export default function RentalDetailsDrawer({ open, query, onClose, onReturn }) {
  const details = query.data
  const isLegacy = details?.collectionVerified === false
  return <Drawer title="Chi tiết đơn thuê" open={open} onClose={onClose} size="large">
    {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
    {query.isError && <div className="rental-details-state"><Alert type="error" showIcon message="Không thể mở chi tiết" description={getDetailsError(query.error)} action={<Button size="small" icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button>} /></div>}
    {!query.isLoading && !query.isError && !details && <Empty description="Không tìm thấy nhóm đơn thuê." />}
    {!query.isLoading && !query.isError && details && <div className="rental-details-stack">
      {isLegacy && <Alert type="warning" showIcon message="Lịch sử cũ chưa xác minh" description="Dữ liệu cũ chưa có xác nhận thu tiền. Hệ thống chỉ hiển thị lịch sử và không cho quyết toán cọc." />}
      <Descriptions column={1} bordered size="small" title="Thông tin đơn thuê">
        <Descriptions.Item label="Mã đơn"><span className="rental-reference-value">{details.orderId || '-'}</span></Descriptions.Item>
        <Descriptions.Item label="Mã sản phẩm"><span className="rental-reference-value">{details.productId || '-'}</span></Descriptions.Item>
        <Descriptions.Item label="Sản phẩm">{details.productName || '-'}</Descriptions.Item>
        <Descriptions.Item label="Khách hàng">{details.customerName || '-'}</Descriptions.Item>
        <Descriptions.Item label="Số điện thoại">{details.customerPhone || '-'}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái đơn">{formatOrderStatus(details.orderStatus)}</Descriptions.Item>
        <Descriptions.Item label="Thuê sớm nhất">{formatDate(details.rentTime)}</Descriptions.Item>
        <Descriptions.Item label="Trả hoặc xử lý gần nhất">{formatDate(details.returnTime)}</Descriptions.Item>
        <Descriptions.Item label="Phí thuê nhóm sản phẩm">{formatAmount(details.rentalFeeTotal)}</Descriptions.Item>
        <Descriptions.Item label="Đã thu toàn đơn">{formatAmount(details.orderCollectedAmount)}</Descriptions.Item>
        <Descriptions.Item label="Cọc đã hoàn của nhóm">{formatAmount(details.refundedDepositAmount)}</Descriptions.Item>
        <Descriptions.Item label="Cọc đã giữ của nhóm">{formatAmount(details.retainedDepositAmount)}</Descriptions.Item>
        <Descriptions.Item label="Cọc còn chờ quyết toán của nhóm">{formatAmount(details.outstandingDepositAmount)}</Descriptions.Item>
      </Descriptions>
      <div className="rental-quantity-summary" aria-label="Tổng số lượng thuê">
        <span><strong>{details.rentedQuantity}</strong> đã thuê</span>
        <span><strong>{details.returnedQuantity}</strong> trả bình thường</span>
        <span><strong>{details.damagedQuantity || 0}</strong> hư hỏng</span>
        <span><strong>{details.lostQuantity || 0}</strong> thất lạc</span>
        <span><strong>{details.outstandingQuantity}</strong> còn lại</span>
        <span><strong>{details.resolvedQuantity || 0}</strong> đã xử lý</span>
      </div>
      {details.outstandingQuantity > 0 && !isLegacy && <Button type="primary" onClick={() => onReturn(details)}>Nhận lại và quyết toán</Button>}
      <TransactionHistory transactions={details.transactions} />
      <Typography.Title level={5}>Lịch sử đơn vị thuê</Typography.Title>
      {(details.rentals || []).length === 0 && <Empty description="Chưa có lịch sử thuê." />}
      <div className="rental-history-list">
        {(details.rentals || []).map((record, index) => {
          const [color, label] = rentalStatus(record)
          return <article className="rental-history-record" key={record.id || `${record.rentTime}-${record.returnTime}-${index}`}>
            <div className="rental-history-record-heading"><strong>Đơn vị {index + 1}</strong><Tag color={color}>{label}</Tag></div>
            <dl>
              <div><dt>Thuê lúc</dt><dd>{formatDate(record.rentTime)}</dd></div>
              <div><dt>Trả hoặc xử lý lúc</dt><dd>{formatDate(record.resolvedAtUtc || record.returnTime)}</dd></div>
              <div><dt>Phí thuê đã chốt</dt><dd>{formatAmount(record.feeAmount)}</dd></div>
              <div><dt>Cọc đã chốt</dt><dd>{formatAmount(record.depositAmount)}</dd></div>
              <div><dt>Mã quyết toán</dt><dd><span className="rental-reference-value">{record.settlementId || '-'}</span></dd></div>
            </dl>
          </article>
        })}
      </div>
    </div>}
  </Drawer>
}
