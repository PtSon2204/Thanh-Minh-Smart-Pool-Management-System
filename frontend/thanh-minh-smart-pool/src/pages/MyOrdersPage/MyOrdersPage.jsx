import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Empty, Pagination, Skeleton, Tag, message } from 'antd'
import { CheckCircleFilled, ClockCircleOutlined, CreditCardOutlined, FileTextOutlined, RightOutlined, ShoppingOutlined } from '@ant-design/icons'
import ticketService from '../../features/tickets/services/ticketService'
import useOnlineTicketPayment from '../../features/payments/hooks/useOnlineTicketPayment'
import OnlineTicketPaymentModal from '../../features/payments/components/OnlineTicketPaymentModal'
import { useCartStore } from '../../features/cart/store/cartStore'
import './MyOrdersPage.css'

const currency = (value) => `${new Intl.NumberFormat('vi-VN').format(value ?? 0)} đ`
const dateTime = (value) => value
  ? new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  : 'Không rõ thời gian'

const filters = [
  { label: 'Tất cả', value: 'ALL' },
  { label: 'Chờ thanh toán', value: 'WAITING' },
  { label: 'Hoàn tất', value: 'COMPLETED' },
  { label: 'Đã hủy', value: 'CANCELLED' },
]

function getStatus(status) {
  if (status === 'PARTIAL') return { label: 'Còn thiếu thanh toán', tone: 'partial' }
  if (status === 'PENDING') return { label: 'Chờ thanh toán', tone: 'pending' }
  if (status === 'COMPLETED') return { label: 'Hoàn tất', tone: 'complete' }
  return { label: 'Đã hủy', tone: 'cancelled' }
}

function OrderProgress({ status }) {
  const completed = status === 'COMPLETED'
  return <div className={`my-order-progress${completed ? ' is-complete' : ''}`} aria-label={completed ? 'Đơn hàng hoàn tất' : 'Đang chờ thanh toán'}>
    <div className="my-order-progress-line" />
    <div className="my-order-progress-step is-done"><span><CheckCircleFilled /></span><small>Đã tạo đơn</small></div>
    <div className={`my-order-progress-step${completed ? ' is-done' : ' is-current'}`}><span>{completed ? <CheckCircleFilled /> : '02'}</span><small>Thanh toán</small></div>
    <div className={`my-order-progress-step${completed ? ' is-done' : ''}`}><span>{completed ? <CheckCircleFilled /> : '03'}</span><small>Phát hành vé</small></div>
  </div>
}

function OrderCard({ order, onResume, onViewTickets, resuming }) {
  const [now] = useState(() => Date.now())
  const state = getStatus(order.status)
  const waiting = order.status === 'PENDING' || order.status === 'PARTIAL'
  const expired = order.expiresAtUtc && Date.parse(order.expiresAtUtc) <= now
  const quantity = order.items?.reduce((total, item) => total + item.quantity, 0) ?? 0
  const previewItems = order.items?.slice(0, 3) ?? []

  return <article className={`my-order-card status-${state.tone}`}>
    <header className="my-order-card-header">
      <div className="my-order-card-identity">
        <span className="my-order-icon"><FileTextOutlined /></span>
        <div>
          <span className="my-order-eyebrow">ĐƠN HÀNG · {order.status}</span>
          <h2>Đơn hàng #{order.orderId.slice(0, 8).toUpperCase()}</h2>
          <div className="my-order-meta"><span><ClockCircleOutlined /> {dateTime(order.createdAt)}</span><span className="my-order-quantity">{quantity} vé</span></div>
        </div>
      </div>
      <div className="my-order-total">
        <strong>{currency(order.finalAmount)}</strong>
        <Tag className={`my-order-status-tag ${state.tone}`}>{state.label}</Tag>
      </div>
    </header>

    <div className="my-order-items">
      {previewItems.map((item) => <div className="my-order-item" key={item.ticketTypeId}>
        <span className="my-order-item-icon"><ShoppingOutlined /></span>
        <span className="my-order-item-copy"><strong>{item.name}</strong><small>{item.quantity} × {currency(item.unitPrice)}</small></span>
        <strong className="my-order-item-total">{currency(item.lineTotal)}</strong>
      </div>)}
      {order.items?.length > 3 && <span className="my-order-more">+{order.items.length - 3} loại vé khác</span>}
    </div>

    {order.status === 'CANCELLED'
      ? <div className="my-order-ended"><span>Đơn hàng đã kết thúc</span><span>Đơn hết hạn không thể tiếp tục thanh toán.</span></div>
      : <OrderProgress status={order.status} />}

    {waiting && order.paidAmount > 0 && <div className="my-order-payment-note">
      Đã nhận {currency(order.paidAmount)} · Còn thiếu <strong>{currency(order.remainingAmount)}</strong>
    </div>}

    <footer className="my-order-card-footer">
      {waiting
        ? <Button type="primary" icon={<CreditCardOutlined />} loading={resuming} disabled={Boolean(expired)} onClick={() => onResume(order)}>
            {expired ? 'Đang cập nhật trạng thái' : 'Tiếp tục thanh toán'}
          </Button>
        : order.status === 'COMPLETED'
          ? <span className="my-order-complete-note"><CheckCircleFilled /> Vé đã được phát hành</span>
          : <span className="my-order-cancel-note">Đơn đã hết hạn hoặc được hủy</span>}
      {waiting && !expired && <span className="my-order-deadline">Hạn thanh toán: {dateTime(order.expiresAtUtc)}</span>}
      {waiting && expired && <span className="my-order-deadline">Đang xác nhận trạng thái đơn…</span>}
      {order.status === 'COMPLETED' && <Button type="link" onClick={onViewTickets}>Xem vé <RightOutlined /></Button>}
    </footer>
  </article>
}

export default function MyOrdersPage() {
  const [status, setStatus] = useState('ALL')
  const [pageIndex, setPageIndex] = useState(1)
  const [resumingId, setResumingId] = useState(null)
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const payment = useOnlineTicketPayment()
  const ordersQuery = useQuery({
    queryKey: ['myOrders', status, pageIndex],
    queryFn: ({ signal }) => ticketService.getMyOrders({ pageIndex, pageSize: 10, status, signal }),
    refetchInterval: (query) => query.state.data?.items?.some((order) => order.status === 'PENDING' || order.status === 'PARTIAL') ? 5000 : false,
    refetchOnWindowFocus: true,
  })
  const orders = ordersQuery.data?.items ?? []

  const refreshOrderData = () => {
    queryClient.invalidateQueries({ queryKey: ['myOrders'] })
    queryClient.invalidateQueries({ queryKey: ['myOrderSummary'] })
  }

  const resumeOrder = async (order) => {
    setResumingId(order.orderId)
    try {
      await payment.resume(order.orderId)
    } catch (error) {
      message.error(error?.response?.data?.message || 'Không thể tải đơn hàng. Vui lòng thử lại.')
      refreshOrderData()
    } finally {
      setResumingId(null)
    }
  }

  const closePayment = () => {
    if (payment.order?.status === 'COMPLETED' || payment.order?.status === 'CANCELLED') {
      payment.finish()
      refreshOrderData()
      if (payment.order?.status === 'COMPLETED') queryClient.invalidateQueries({ queryKey: ['myTickets'] })
    } else {
      payment.close()
    }
  }

  const returnToCart = () => {
    payment.finish()
    refreshOrderData()
    navigate('/bang-gia')
    useCartStore.setState({ isCartOpen: true })
  }

  const finishPayment = () => {
    payment.finish()
    refreshOrderData()
    queryClient.invalidateQueries({ queryKey: ['myTickets'] })
    navigate('/the-cua-toi')
  }

  return <main className="my-orders-page">
    <div className="my-orders-container">
      <div className="my-orders-heading">
        <div>
          <span className="my-orders-kicker">THANH MINH · DỊCH VỤ VÉ</span>
          <h1>Đơn hàng của bạn</h1>
          <p>Theo dõi thanh toán và tiếp tục đơn còn dang dở.</p>
        </div>
        {(ordersQuery.data?.unpaidCount ?? 0) > 0 && <div className="my-orders-open-count">
          <span>{ordersQuery.data.unpaidCount}</span><small>đơn cần thanh toán</small>
        </div>}
      </div>

      <div className="my-orders-filter-row" role="tablist" aria-label="Lọc đơn hàng">
        {filters.map((item) => <button key={item.value} type="button" role="tab" aria-selected={status === item.value}
          className={status === item.value ? 'is-active' : ''} onClick={() => { setStatus(item.value); setPageIndex(1) }}>
          {item.label}{item.value === 'WAITING' && (ordersQuery.data?.unpaidCount ?? 0) > 0 && <span className="my-orders-filter-count">{ordersQuery.data.unpaidCount}</span>}
        </button>)}
      </div>

      {ordersQuery.isError && <Alert className="my-orders-error" type="error" showIcon message="Không tải được danh sách đơn hàng" description={ordersQuery.error?.response?.data?.message || 'Kiểm tra kết nối rồi tải lại trang.'} action={<Button onClick={() => ordersQuery.refetch()}>Thử lại</Button>} />}

      {ordersQuery.isLoading
        ? <div className="my-orders-skeletons"><Skeleton active /><Skeleton active /></div>
        : ordersQuery.isError && !ordersQuery.data
          ? null
        : orders.length > 0
          ? <>
              <div className="my-orders-list">{orders.map((order) => <OrderCard key={order.orderId} order={order}
                onResume={resumeOrder} onViewTickets={() => navigate('/the-cua-toi')} resuming={resumingId === order.orderId} />)}</div>
              {ordersQuery.data.totalCount > 10 && <Pagination current={pageIndex} pageSize={10} total={ordersQuery.data.totalCount}
                onChange={setPageIndex} showSizeChanger={false} className="my-orders-pagination" />}
            </>
          : <section className="my-orders-empty">
              <Empty image={<span className="my-orders-empty-icon"><FileTextOutlined /></span>}
                description={<><strong>{status === 'ALL' ? 'Chưa có đơn hàng nào' : 'Không có đơn trong mục này'}</strong><span>{status === 'ALL' ? 'Đơn hàng sẽ xuất hiện ở đây sau khi bạn bắt đầu thanh toán.' : 'Thử chọn bộ lọc khác để xem các đơn hàng.'}</span></>}>
                {status === 'ALL' && <Button type="primary" onClick={() => navigate('/bang-gia')}>Chọn vé</Button>}
              </Empty>
            </section>}
    </div>

    <OnlineTicketPaymentModal payment={payment} onClose={closePayment} onFinish={finishPayment} onReturnToCart={returnToCart} />
  </main>
}
