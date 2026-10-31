import { Button, Drawer, Empty, Pagination, Spin, Table, Tag } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { useState } from 'react'
import { useRentalPaymentQueue } from '../hooks/useRentalPayments'
import { requiresRentalPaymentReconciliation } from '../state/rentalPaymentState'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function queueStatus(payment) {
  if (payment.status === 'PAID') return ['success', 'Đã thu đủ']
  if (requiresRentalPaymentReconciliation(payment)) return ['warning', 'Cần đối soát']
  if (payment.status === 'PARTIAL') return ['processing', 'Đã thu một phần']
  if (payment.status === 'PENDING') return ['processing', 'Chờ thanh toán']
  return ['default', payment.status]
}

export default function RentalPaymentQueueDrawer({ open, onClose, onOpenPayment }) {
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const query = useRentalPaymentQueue({ pageIndex, pageSize }, open)
  const columns = [
    { title: 'Khách hàng', key: 'customer', render: (_, payment) => <div className="rental-order-cell"><strong>{payment.customerName}</strong><span>{payment.customerPhone || '-'}</span></div> },
    { title: 'Trạng thái', key: 'status', render: (_, payment) => <Tag color={queueStatus(payment)[0]}>{queueStatus(payment)[1]}</Tag> },
    { title: 'Còn thiếu', dataIndex: 'remainingAmount', align: 'right', render: (value) => currencyFormatter.format(value || 0) },
    { title: 'Thao tác', key: 'action', align: 'right', render: (_, payment) => <Button type="link" onClick={() => onOpenPayment(payment.id)}>Mở</Button> },
  ]

  return <Drawer rootClassName="rental-payment-queue-drawer" title="Yêu cầu QR cần xử lý" open={open} onClose={onClose} size="large">
    {query.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
    {query.isError && <Empty description="Không thể tải hàng đợi thanh toán QR."><Button icon={<ReloadOutlined />} onClick={query.refetch}>Thử lại</Button></Empty>}
    {!query.isLoading && !query.isError && <>
      <Table rowKey="id" columns={columns} dataSource={query.data?.items || []} pagination={false} locale={{ emptyText: 'Không có yêu cầu QR cần xử lý.' }} />
      {(query.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={query.data.totalCount} showSizeChanger pageSizeOptions={['20', '50']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} /></div>}
    </>}
  </Drawer>
}
