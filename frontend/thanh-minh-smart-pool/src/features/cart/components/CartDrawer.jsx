import { useState } from 'react'
import { Drawer, Button, Typography, Divider, Select, message } from 'antd'
import { DeleteOutlined, MinusOutlined, PlusOutlined, SendOutlined } from '@ant-design/icons'
import { useCartStore } from '../store/cartStore'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { useVouchers } from '../../vouchers/hooks/useVouchers'
import { useVoucherClock } from '../../vouchers/hooks/useVoucherClock'
import { getVoucherDiscount, getVoucherError, VoucherStatus } from '../../vouchers/utils/voucherValidity'
import useOnlineTicketPayment from '../../payments/hooks/useOnlineTicketPayment'
import OnlineTicketPaymentModal from '../../payments/components/OnlineTicketPaymentModal'
import './CartDrawer.css'

const { Text, Title } = Typography

export default function CartDrawer() {
  const { isCartOpen, closeCart, items, removeFromCart, updateQuantity, removePurchasedItems } = useCartStore()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedVoucherCode, setSelectedVoucherCode] = useState(null)

  const payment = useOnlineTicketPayment()

  const vouchersQuery = useVouchers({ status: VoucherStatus.ACTIVE, pageIndex: 1, pageSize: 100 }, { enabled: isCartOpen })
  const vouchers = vouchersQuery.data?.items || []
  const now = useVoucherClock(isCartOpen)

  const totalPrice = items.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const totalQuantity = items.reduce((sum, item) => sum + item.quantity, 0)

  const selectedVoucher = vouchers.find(v => v.code === selectedVoucherCode)
  const selectedVoucherError = selectedVoucherCode ? getVoucherError(selectedVoucher, totalPrice, now) : null
  const discountAmount = getVoucherDiscount(selectedVoucher, totalPrice, now)

  const finalPrice = Math.max(0, totalPrice - discountAmount)

  const handleCheckout = async () => {
    if (payment.checkingOut) return
    if (!payment.hasOrder && selectedVoucherCode) {
      const error = getVoucherError(selectedVoucher, totalPrice)
      if (error) {
        message.error(error)
        setSelectedVoucherCode(null)
        vouchersQuery.refetch()
        return
      }
    }
    try {
      const payload = {
        items: items.map(i => ({ ticketTypeId: i.id, quantity: i.quantity })),
        voucherCode: selectedVoucherCode || null
      }
      await payment.start(payload)
      closeCart()
    } catch (error) {
      message.error(error.response?.data?.message || error.message || 'Lỗi khi thanh toán')
    }
  }

  const completePayment = () => {
    removePurchasedItems(payment.order?.items || [])
    setSelectedVoucherCode(null)
    payment.finish()
    queryClient.invalidateQueries({ queryKey: ['myTickets'] })
  }

  const handleFinishPayment = () => {
    completePayment()
    closeCart()
    navigate('/the-cua-toi')
  }

  const handleClosePayment = () => {
    if (payment.order?.status === 'COMPLETED') completePayment()
    else if (payment.order?.status === 'CANCELLED') payment.finish()
    else payment.close()
  }

  const handleReturnToCart = () => {
    payment.finish()
    useCartStore.setState({ isCartOpen: true })
  }

  const validVouchers = vouchers.filter(v => !getVoucherError(v, totalPrice, now))

  return (
    <>
      <Drawer
        title={
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div className="cart-drawer-icon">🛒</div>
            <div>
              <div style={{ fontWeight: 700, fontSize: '18px', color: '#002c8c' }}>Giỏ hàng của bạn</div>
              <div style={{ fontSize: '13px', color: '#666', fontWeight: 400 }}>{totalQuantity} sản phẩm trong giỏ</div>
            </div>
          </div>
        }
        placement="right"
        onClose={closeCart}
        open={isCartOpen}
        width={400}
        className="custom-cart-drawer"
      >
        <div className="cart-items-container">
          {items.length === 0 ? (
            <div className="empty-cart">
              <Text type="secondary">Giỏ hàng trống.</Text>
            </div>
          ) : (
            items.map((item) => (
              <div key={item.id} className="cart-item">
                <div className="cart-item-image-wrapper">
                  <img src={item.image || 'https://via.placeholder.com/60'} alt={item.name} className="cart-item-image" />
                </div>
                <div className="cart-item-details">
                  <div className="cart-item-header">
                    <Text strong className="cart-item-title">{item.name}</Text>
                    <Button type="text" icon={<DeleteOutlined />} onClick={() => removeFromCart(item.id)} size="small" danger />
                  </div>
                  <div className="cart-item-footer">
                    <Text strong style={{ color: '#0088cc', fontSize: '15px' }}>
                      {new Intl.NumberFormat('vi-VN').format(item.price)} đ
                    </Text>
                    <div className="quantity-control">
                      <button onClick={() => updateQuantity(item.id, -1)}><MinusOutlined style={{ fontSize: 10 }} /></button>
                      <span>{item.quantity}</span>
                      <button onClick={() => updateQuantity(item.id, 1)}><PlusOutlined style={{ fontSize: 10 }} /></button>
                    </div>
                  </div>
                </div>
              </div>
            ))
          )}
        </div>

        {(items.length > 0 || payment.hasOrder) && (
          <div className="cart-summary">
            <Divider style={{ margin: '16px 0' }} />
            
            <div style={{ marginBottom: '16px' }}>
              <Text strong style={{ display: 'block', marginBottom: '8px' }}>Mã giảm giá:</Text>
              <Select
                allowClear
                placeholder="Chọn mã giảm giá"
                notFoundContent="Hiện không có mã giảm giá nào"
                style={{ width: '100%' }}
                value={selectedVoucherError ? null : selectedVoucherCode}
                onChange={setSelectedVoucherCode}
                loading={vouchersQuery.isFetching}
                options={validVouchers.map(v => ({
                  value: v.code,
                  label: `${v.code} - Giảm ${v.discountType === 'PERCENTAGE' ? v.discountValue + '%' : new Intl.NumberFormat('vi-VN').format(v.discountValue) + 'đ'}`
                }))}
              />
              {selectedVoucherError && <Text type="danger">{selectedVoucherError}</Text>}
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <Text style={{ color: '#666' }}>Tạm tính:</Text>
              <Text>{new Intl.NumberFormat('vi-VN').format(totalPrice)} đ</Text>
            </div>

            {discountAmount > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                <Text style={{ color: '#52c41a' }}>Giảm giá:</Text>
                <Text style={{ color: '#52c41a' }}>- {new Intl.NumberFormat('vi-VN').format(discountAmount)} đ</Text>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', marginTop: '8px' }}>
              <Text style={{ fontSize: '16px', color: '#666', fontWeight: 600 }}>Tổng tiền:</Text>
              <Title level={4} style={{ margin: 0, color: '#f97316' }}>
                {new Intl.NumberFormat('vi-VN').format(finalPrice)} đ
              </Title>
            </div>
            <Button 
              type="primary" 
              size="large" 
              block 
              icon={<SendOutlined />}
              onClick={handleCheckout} loading={payment.checkingOut}
              className="checkout-btn"
            >
              {payment.hasOrder ? 'Tiếp tục thanh toán' : 'Thanh Toán Ngay'}
            </Button>
            <div style={{ textAlign: 'center', marginTop: '12px' }}>
              <Text type="secondary" style={{ fontSize: '12px' }}>
                <span style={{ color: '#52c41a' }}>✓</span> Giao dịch an toàn • Hỗ trợ 24/7
              </Text>
            </div>
          </div>
        )}
      </Drawer>

      <OnlineTicketPaymentModal payment={payment} onClose={handleClosePayment}
        onFinish={handleFinishPayment} onReturnToCart={handleReturnToCart} />
    </>
  )
}
