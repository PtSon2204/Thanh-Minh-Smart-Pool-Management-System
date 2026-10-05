import { Alert, Button, Descriptions, Drawer, Empty, Spin, Tag, Typography } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
}

function getDetailsError(error) {
  const status = error?.response?.status
  if (status === 404) return 'Nhóm đơn thuê này không còn tồn tại.'
  if (status === 401 || status === 403) return 'Bạn không có quyền xem chi tiết đơn thuê này.'
  return error?.response?.data?.message || 'Không thể tải chi tiết đơn thuê. Hãy thử lại.'
}

function formatOrderStatus(value) {
  if (value === 'Renting') return 'Đang thuê'
  if (value === 'Returned') return 'Đã trả hết'
  return value || '-'
}

export default function RentalDetailsDrawer({ open, query, onClose, onReturn }) {
  const details = query.data
  return <Drawer title="Chi tiết đơn thuê" open={open} onClose={onClose} size="large">
    {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
    {query.isError && <div className="rental-details-state"><Alert type="error" showIcon message="Không thể mở chi tiết" description={getDetailsError(query.error)} action={<Button size="small" icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button>} /></div>}
    {!query.isLoading && !query.isError && !details && <Empty description="Không tìm thấy nhóm đơn thuê." />}
    {!query.isLoading && !query.isError && details && <div className="rental-details-stack">
      <Descriptions column={1} bordered size="small" title="Thông tin đơn thuê">
        <Descriptions.Item label="Mã đơn"><span className="rental-reference-value">{details.orderId || '-'}</span></Descriptions.Item>
        <Descriptions.Item label="Mã sản phẩm"><span className="rental-reference-value">{details.productId || '-'}</span></Descriptions.Item>
        <Descriptions.Item label="Sản phẩm">{details.productName || '-'}</Descriptions.Item>
        <Descriptions.Item label="Khách hàng">{details.customerName || '-'}</Descriptions.Item>
        <Descriptions.Item label="Số điện thoại">{details.customerPhone || '-'}</Descriptions.Item>
        <Descriptions.Item label="Trạng thái đơn">{formatOrderStatus(details.orderStatus)}</Descriptions.Item>
        <Descriptions.Item label="Thuê sớm nhất">{formatDate(details.rentTime)}</Descriptions.Item>
        <Descriptions.Item label="Trả gần nhất">{formatDate(details.returnTime)}</Descriptions.Item>
        <Descriptions.Item label="Tổng cọc đã ghi nhận">{currencyFormatter.format(details.totalDepositAmount || 0)}</Descriptions.Item>
      </Descriptions>
      <div className="rental-quantity-summary" aria-label="Tổng số lượng thuê">
        <span><strong>{details.rentedQuantity}</strong> đã thuê</span>
        <span><strong>{details.returnedQuantity}</strong> đã trả</span>
        <span><strong>{details.outstandingQuantity}</strong> còn lại</span>
      </div>
      {details.outstandingQuantity > 0 && <Button type="primary" onClick={() => onReturn(details)}>Nhận lại</Button>}
      <Typography.Title level={5}>Lịch sử trả hàng</Typography.Title>
      {(details.rentals || []).length === 0 && <Empty description="Chưa có lịch sử thuê." />}
      <div className="rental-history-list">
        {(details.rentals || []).map((record, index) => <article className="rental-history-record" key={`${record.rentTime}-${record.returnTime}-${index}`}>
          <div className="rental-history-record-heading"><strong>Lượt thuê {index + 1}</strong><Tag color={record.status === 'Returned' ? 'success' : 'processing'}>{record.status === 'Returned' ? 'Đã trả' : 'Đang thuê'}</Tag></div>
          <dl>
            <div><dt>Thuê lúc</dt><dd>{formatDate(record.rentTime)}</dd></div>
            <div><dt>Trả lúc</dt><dd>{formatDate(record.returnTime)}</dd></div>
            <div><dt>Cọc đã ghi nhận</dt><dd>{record.depositAmount == null ? '-' : currencyFormatter.format(record.depositAmount)}</dd></div>
          </dl>
        </article>)}
      </div>
    </div>}
  </Drawer>
}
