import { useState } from 'react'
import { EyeOutlined, PlusOutlined, QrcodeOutlined, ReloadOutlined, RollbackOutlined } from '@ant-design/icons'
import { Button, DatePicker, Empty, Pagination, Select, Space, Spin, Table, Tag, Tooltip } from 'antd'
import dayjs from 'dayjs'
import { ServiceListHeader } from "../../../features/services/components/ServiceListChrome"
import RentalCheckoutDrawer from "../../../features/rentals/components/RentalCheckoutDrawer"
import RentalDetailsDrawer from "../../../features/rentals/components/RentalDetailsDrawer"
import RentalPaymentQueueDrawer from "../../../features/rentals/components/RentalPaymentQueueDrawer"
import RentalQrPaymentDrawer from "../../../features/rentals/components/RentalQrPaymentDrawer"
import RentalReturnDrawer from "../../../features/rentals/components/RentalReturnDrawer"
import { useCheckoutRental } from "../../../features/rentals/hooks/useRentalMutations"
import { useRentalPayment } from "../../../features/rentals/hooks/useRentalPayments"
import { useRentalDetails, useRentals } from "../../../features/rentals/hooks/useRentals"
import "../../../features/services/styles/serviceManagement.css"
import "../../../features/rentals/styles/rentalManagement.css"
import "../../../components/shared/styles/adminActions.css"

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ description, onRetry }) {
  return <Empty description={description}><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
}

function groupStatus(rental) {
  if (rental.outstandingQuantity > 0) return ['processing', 'Đang thuê']
  if ((rental.damagedQuantity || 0) > 0 || (rental.lostQuantity || 0) > 0) return ['warning', 'Đã xử lý']
  return ['success', 'Trả bình thường']
}

function RentalActions({ rental, onDetails, onReturn, disabled }) {
  return <>
    <Tooltip title="Xem chi tiết" trigger={['hover', 'focus']}>
      <Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} aria-label={`Xem chi tiết ${rental.productName}`} onClick={() => onDetails(rental)} />
    </Tooltip>
    {rental.outstandingQuantity > 0 && <Tooltip title={rental.collectionVerified === false ? 'Chưa xác minh thu tiền' : 'Nhận lại'} trigger={['hover', 'focus']}>
      <Button className="admin-action-button admin-action-button--positive" icon={<RollbackOutlined />} aria-label={`Nhận lại ${rental.productName}`} disabled={disabled || rental.collectionVerified === false} onClick={() => onReturn(rental)} />
    </Tooltip>}
  </>
}

function RentalMobileCards({ rentals, onDetails, onReturn, disabled }) {
  return <div className="rental-mobile-list">
    {rentals.map((rental) => <article className="rental-mobile-card" key={`${rental.orderId}-${rental.productId}`}>
      <div className="rental-mobile-card-heading"><strong>{rental.productName}</strong><Tag color={groupStatus(rental)[0]}>{groupStatus(rental)[1]}</Tag></div>
      <span>Mã đơn: {rental.orderId.slice(0, 8).toUpperCase()}</span>
      {rental.collectionVerified === false && <Tag color="warning">Chưa xác minh thu tiền</Tag>}
      <div className="rental-quantity-cell"><span><strong>{rental.quantity}</strong> đã thuê</span><span><strong>{rental.returnedQuantity}</strong> bình thường</span><span><strong>{rental.damagedQuantity || 0}</strong> hư hỏng</span><span><strong>{rental.lostQuantity || 0}</strong> thất lạc</span><span><strong>{rental.outstandingQuantity}</strong> còn lại</span></div>
      <div className="admin-action-group rental-mobile-card-actions rental-icon-actions"><RentalActions rental={rental} onDetails={onDetails} onReturn={onReturn} disabled={disabled} /></div>
    </article>)}
  </div>
}

export default function RentalPage() {
  const [status, setStatus] = useState()
  const [date, setDate] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [checkoutOpen, setCheckoutOpen] = useState(false)
  const [detailsRental, setDetailsRental] = useState(null)
  const [returnRequested, setReturnRequested] = useState(false)
  const [paymentQueueOpen, setPaymentQueueOpen] = useState(false)
  const rentalsQuery = useRentals({ status, date, pageIndex, pageSize, groupByOrder: true })
  const detailsQuery = useRentalDetails(detailsRental?.orderId, detailsRental?.productId, Boolean(detailsRental))
  const checkoutMutation = useCheckoutRental()
  const paymentState = useRentalPayment()

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const isPending = checkoutMutation.isPending || paymentState.creating || paymentState.issuing

  const columns = [
    {
      title: 'Đơn thuê', dataIndex: 'productName', key: 'productName', width: '30%',
      render: (value, rental) => <div className="rental-order-cell"><Tooltip title={value}><strong className="rental-product-name">{value}</strong></Tooltip><span>Mã đơn: {rental.orderId.slice(0, 8).toUpperCase()}</span>{rental.customerName && <span>{rental.customerName}{rental.customerPhone ? ` · ${rental.customerPhone}` : ''}</span>}</div>,
    },
     { title: 'Số lượng', key: 'quantity', width: '24%', render: (_, rental) => <div className="rental-quantity-cell"><span><strong>{rental.quantity}</strong> đã thuê</span><span><strong>{rental.returnedQuantity}</strong> bình thường</span><span><strong>{rental.damagedQuantity || 0}</strong> hư hỏng</span><span><strong>{rental.lostQuantity || 0}</strong> thất lạc</span><span><strong>{rental.outstandingQuantity}</strong> còn lại</span></div> },
     { title: 'Thuê lúc', dataIndex: 'rentTime', key: 'rentTime', width: '11%', render: formatDate },
     { title: 'Cọc nhóm SP', dataIndex: 'depositAmount', key: 'depositAmount', width: '11%', align: 'right', render: (value) => value == null ? '-' : <span className="service-number-cell">{currencyFormatter.format(value)}</span> },
     { title: 'Trạng thái', dataIndex: 'status', key: 'status', width: '12%', render: (_, rental) => <div className="rental-status-cell"><Tag color={groupStatus(rental)[0]}>{groupStatus(rental)[1]}</Tag>{rental.orderStatus === 'Completed' && <Tag color="success">Đơn hoàn tất</Tag>}{rental.collectionVerified === false && <Tag color="warning">Chưa xác minh thu tiền</Tag>}{rental.outstandingQuantity > 0 && <span>Còn {rental.outstandingQuantity}</span>}</div> },
     {
        title: 'Thao tác', key: 'actions', width: '12%', align: 'right', render: (_, rental) => <div className="admin-action-group rental-row-actions rental-icon-actions"><RentalActions rental={rental} onDetails={setDetailsRental} onReturn={(selected) => { setDetailsRental(selected); setReturnRequested(true) }} disabled={isPending} /></div>,
    },
  ]

  return (
    <div className="service-list-page rental-list-page">
      <ServiceListHeader title="Giao dịch cho thuê" action={<Space wrap><Button icon={<QrcodeOutlined />} onClick={() => setPaymentQueueOpen(true)}>Yêu cầu QR</Button><Button type="primary" icon={<PlusOutlined />} disabled={paymentState.hasUncertainOutcome} onClick={() => { checkoutMutation.reset(); setCheckoutOpen(true) }}>Tạo đơn thuê</Button></Space>} />
      <section className="service-list-toolbar" aria-label="Bộ lọc giao dịch cho thuê">
        <div className="service-list-filters">
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear aria-label="Lọc theo trạng thái thuê" placeholder="Tất cả trạng thái" disabled={isPending} value={status} onChange={resetPage(setStatus)} options={[{ label: 'Đang thuê', value: 'Renting' }, { label: 'Trả bình thường', value: 'Returned' }, { label: 'Đã xử lý', value: 'Resolved' }, { label: 'Đơn hoàn tất', value: 'Completed' }]} /></label>
        <label className="service-filter-control"><span>Ngày (UTC)</span><DatePicker allowClear aria-label="Lọc theo ngày thuê UTC" placeholder="Chọn ngày UTC" disabled={isPending} value={date ? dayjs(date) : null} onChange={(value) => resetPage(setDate)(value ? value.format('YYYY-MM-DD') : undefined)} /></label>
        </div>
      </section>
      <section className="service-list-table" aria-label="Danh sách giao dịch cho thuê">
        {rentalsQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {rentalsQuery.isError && <RetryEmpty description="Không thể tải danh sách giao dịch cho thuê." onRetry={rentalsQuery.refetch} />}
        {!rentalsQuery.isLoading && !rentalsQuery.isError && <><Table className="rental-table" rowKey={(rental) => `${rental.orderId}-${rental.productId}`} columns={columns} dataSource={rentalsQuery.data?.items || []} pagination={false} tableLayout="fixed" locale={{ emptyText: 'Chưa có giao dịch cho thuê phù hợp.' }} /><RentalMobileCards rentals={rentalsQuery.data?.items || []} disabled={isPending} onDetails={setDetailsRental} onReturn={(rental) => { setDetailsRental(rental); setReturnRequested(true) }} /></>}
        {!rentalsQuery.isLoading && !rentalsQuery.isError && (rentalsQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={rentalsQuery.data.totalCount} disabled={isPending} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} giao dịch`} /></div>}
      </section>
      <RentalCheckoutDrawer key={checkoutOpen ? 'checkout-open' : 'checkout-closed'} open={checkoutOpen} mutation={checkoutMutation} paymentState={paymentState} onClose={() => setCheckoutOpen(false)} onSuccess={() => setCheckoutOpen(false)} />
      <RentalDetailsDrawer open={Boolean(detailsRental) && !returnRequested} query={detailsQuery} onClose={() => setDetailsRental(null)} onReturn={() => setReturnRequested(true)} />
      <RentalReturnDrawer key={returnRequested && detailsRental ? `${detailsRental.orderId}-${detailsRental.productId}` : 'return-closed'} open={returnRequested} details={detailsQuery.data} detailsError={detailsQuery.error} detailsLoading={detailsQuery.isLoading} onClose={() => setReturnRequested(false)} onConflict={() => detailsQuery.refetch()} onRetryDetails={detailsQuery.refetch} />
      <RentalPaymentQueueDrawer open={paymentQueueOpen} onClose={() => setPaymentQueueOpen(false)} onOpenPayment={(paymentId) => { paymentState.openPayment(paymentId); setPaymentQueueOpen(false) }} />
      <RentalQrPaymentDrawer paymentState={paymentState} />
    </div>
  )
}
