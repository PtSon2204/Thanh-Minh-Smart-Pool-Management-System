import { useState } from 'react'
import { EyeOutlined, PlusOutlined, ReloadOutlined, RollbackOutlined } from '@ant-design/icons'
import { Button, DatePicker, Empty, Pagination, Select, Spin, Table, Tag, Tooltip } from 'antd'
import dayjs from 'dayjs'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import RentalCheckoutDrawer from '../../features/rentals/components/RentalCheckoutDrawer'
import RentalDetailsDrawer from '../../features/rentals/components/RentalDetailsDrawer'
import RentalReturnDrawer from '../../features/rentals/components/RentalReturnDrawer'
import { useCheckoutRental } from '../../features/rentals/hooks/useRentalMutations'
import { useRentalDetails, useRentals } from '../../features/rentals/hooks/useRentals'
import '../../features/services/styles/serviceManagement.css'
import '../../features/rentals/styles/rentalManagement.css'
import '../../features/shared/styles/adminActions.css'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ description, onRetry }) {
  return <Empty description={description}><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function formatDate(value) {
  return value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
}

function RentalMobileCards({ rentals, onDetails, onReturn, disabled }) {
  return <div className="rental-mobile-list">
    {rentals.map((rental) => <article className="rental-mobile-card" key={`${rental.orderId}-${rental.productId}`}>
      <div className="rental-mobile-card-heading"><strong>{rental.productName}</strong><Tag color={rental.outstandingQuantity === 0 ? 'success' : 'processing'}>{rental.outstandingQuantity === 0 ? 'Đã trả hết' : 'Đang thuê'}</Tag></div>
      <span>Mã đơn: {rental.orderId.slice(0, 8).toUpperCase()}</span>
      <div className="rental-quantity-cell"><span><strong>{rental.quantity}</strong> đã thuê</span><span><strong>{rental.returnedQuantity}</strong> đã trả</span><span><strong>{rental.outstandingQuantity}</strong> còn lại</span></div>
      <div className="admin-action-group rental-mobile-card-actions"><Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} onClick={() => onDetails(rental)}>Xem chi tiết</Button>{rental.outstandingQuantity > 0 && <Button className="admin-action-button admin-action-button--positive" icon={<RollbackOutlined />} disabled={disabled} onClick={() => onReturn(rental)}>Nhận lại</Button>}</div>
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
  const rentalsQuery = useRentals({ status, date, pageIndex, pageSize, groupByOrder: true })
  const detailsQuery = useRentalDetails(detailsRental?.orderId, detailsRental?.productId, Boolean(detailsRental))
  const checkoutMutation = useCheckoutRental()

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const isPending = checkoutMutation.isPending

  const columns = [
    {
      title: 'Đơn thuê', dataIndex: 'productName', key: 'productName', width: 340,
      render: (value, rental) => <div className="rental-order-cell"><Tooltip title={value}><strong className="rental-product-name">{value}</strong></Tooltip><span>Mã đơn: {rental.orderId.slice(0, 8).toUpperCase()}</span>{rental.customerName && <span>{rental.customerName}{rental.customerPhone ? ` · ${rental.customerPhone}` : ''}</span>}</div>,
    },
    { title: 'Số lượng', key: 'quantity', width: 204, render: (_, rental) => <div className="rental-quantity-cell"><span><strong>{rental.quantity}</strong> đã thuê</span><span><strong>{rental.returnedQuantity}</strong> đã trả</span><span><strong>{rental.outstandingQuantity}</strong> còn lại</span></div> },
    { title: 'Thuê lúc', dataIndex: 'rentTime', key: 'rentTime', width: 160, render: formatDate },
    { title: 'Tiền cọc tổng', dataIndex: 'depositAmount', key: 'depositAmount', width: 150, align: 'right', render: (value) => value == null ? '-' : <span className="service-number-cell">{currencyFormatter.format(value)}</span> },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status', width: 126, render: (_, rental) => <div className="rental-status-cell"><Tag color={rental.outstandingQuantity === 0 ? 'success' : 'processing'}>{rental.outstandingQuantity === 0 ? 'Đã trả hết' : 'Đang thuê'}</Tag>{rental.outstandingQuantity > 0 && <span>Còn {rental.outstandingQuantity}</span>}</div> },
    {
      title: 'Thao tác', key: 'actions', width: 256, fixed: 'right', align: 'right', render: (_, rental) => <div className="admin-action-group rental-row-actions"><Button className="admin-action-button admin-action-button--view" size="small" icon={<EyeOutlined />} onClick={() => setDetailsRental(rental)}>Xem chi tiết</Button>{rental.outstandingQuantity > 0 && <Button className="admin-action-button admin-action-button--positive" size="small" icon={<RollbackOutlined />} disabled={isPending} onClick={() => { setDetailsRental(rental); setReturnRequested(true) }}>Nhận lại</Button>}</div>,
    },
  ]

  return (
    <div className="service-list-page rental-list-page">
      <ServiceListHeader title="Giao dịch cho thuê" action={<Button type="primary" icon={<PlusOutlined />} onClick={() => { checkoutMutation.reset(); setCheckoutOpen(true) }}>Tạo đơn thuê</Button>} />
      <section className="service-list-toolbar" aria-label="Bộ lọc giao dịch cho thuê">
        <div className="service-list-filters">
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear aria-label="Lọc theo trạng thái thuê" placeholder="Tất cả trạng thái" disabled={isPending} value={status} onChange={resetPage(setStatus)} options={[{ label: 'Đang thuê', value: 'Renting' }, { label: 'Đã trả hết', value: 'Returned' }]} /></label>
        <label className="service-filter-control"><span>Ngày (UTC)</span><DatePicker allowClear aria-label="Lọc theo ngày thuê UTC" placeholder="Chọn ngày UTC" disabled={isPending} value={date ? dayjs(date) : null} onChange={(value) => resetPage(setDate)(value ? value.format('YYYY-MM-DD') : undefined)} /></label>
        </div>
      </section>
      <section className="service-list-table" aria-label="Danh sách giao dịch cho thuê">
        {rentalsQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {rentalsQuery.isError && <RetryEmpty description="Không thể tải danh sách giao dịch cho thuê." onRetry={rentalsQuery.refetch} />}
        {!rentalsQuery.isLoading && !rentalsQuery.isError && <><Table className="rental-table" rowKey={(rental) => `${rental.orderId}-${rental.productId}`} columns={columns} dataSource={rentalsQuery.data?.items || []} pagination={false} scroll={{ x: 1128 }} tableLayout="fixed" locale={{ emptyText: 'Chưa có giao dịch cho thuê phù hợp.' }} /><RentalMobileCards rentals={rentalsQuery.data?.items || []} disabled={isPending} onDetails={setDetailsRental} onReturn={(rental) => { setDetailsRental(rental); setReturnRequested(true) }} /></>}
        {!rentalsQuery.isLoading && !rentalsQuery.isError && (rentalsQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={rentalsQuery.data.totalCount} disabled={isPending} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} giao dịch`} /></div>}
      </section>
      <RentalCheckoutDrawer key={checkoutOpen ? 'checkout-open' : 'checkout-closed'} open={checkoutOpen} mutation={checkoutMutation} onClose={() => setCheckoutOpen(false)} onSuccess={() => setCheckoutOpen(false)} />
      <RentalDetailsDrawer open={Boolean(detailsRental) && !returnRequested} query={detailsQuery} onClose={() => setDetailsRental(null)} onReturn={() => setReturnRequested(true)} />
      <RentalReturnDrawer key={returnRequested && detailsRental ? `${detailsRental.orderId}-${detailsRental.productId}` : 'return-closed'} open={returnRequested} details={detailsQuery.data} detailsError={detailsQuery.error} detailsLoading={detailsQuery.isLoading} onClose={() => setReturnRequested(false)} onConflict={() => detailsQuery.refetch()} onRetryDetails={detailsQuery.refetch} />
    </div>
  )
}
