import { useState, useEffect } from 'react'
import { Drawer, Button, Typography, Divider, Select, message, Modal, Result, Spin } from 'antd'
import { DeleteOutlined, MinusOutlined, PlusOutlined, SendOutlined } from '@ant-design/icons'
import { useCartStore } from '../store/cartStore'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import apiClient from '../../../services/apiClient'
import './CartDrawer.css'

const { Text, Title } = Typography

export default function CartDrawer() {
  const { isCartOpen, closeCart, items, removeFromCart, updateQuantity, clearCart } = useCartStore()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [selectedVoucherCode, setSelectedVoucherCode] = useState(null)

  // Payment Modal State
  const [paymentModalOpen, setPaymentModalOpen] = useState(false)
  const [paymentInfo, setPaymentInfo] = useState(null)
  const [paymentStatus, setPaymentStatus] = useState('pending') // pending | partial | success
  const [partialInfo, setPartialInfo] = useState(null) // { paidAmount, remainingAmount }

  const { data: vouchersData } = useQuery({
    queryKey: ['vouchers'],
    queryFn: async () => {
      const res = await apiClient.get('/api/vouchers?PageNumber=1&PageSize=100')
      return res.data.data
    },
    enabled: isCartOpen
  })

  const totalPrice = items.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const totalQuantity = items.reduce((sum, item) => sum + item.quantity, 0)

  const selectedVoucher = vouchersData?.find(v => v.code === selectedVoucherCode)
  let discountAmount = 0
  
  if (selectedVoucher) {
    if (!selectedVoucher.minOrderValue || totalPrice >= selectedVoucher.minOrderValue) {
      if (selectedVoucher.discountType === 'PERCENTAGE') {
        discountAmount = (totalPrice * selectedVoucher.discountValue) / 100
      } else {
        discountAmount = selectedVoucher.discountValue
      }
    }
  }

  const finalPrice = Math.max(0, totalPrice - discountAmount)

  const [checkingOut, setCheckingOut] = useState(false)

  const handleCheckout = async () => {
    if (checkingOut) return;
    try {
      setCheckingOut(true)
      const payload = {
        items: items.map(i => ({ ticketTypeId: i.id, quantity: i.quantity })),
        voucherCode: selectedVoucherCode || null
      }
      const res = await apiClient.post('/api/tickets/checkout', payload)
      
      setPaymentInfo({
        orderId: res.data.orderId,
        transactionRef: res.data.transactionRef,
        amount: res.data.totalAmount
      })
      setPaymentStatus('pending')
      setPaymentModalOpen(true)
    } catch (error) {
      message.error(error.response?.data?.message || 'Lỗi khi thanh toán')
    } finally {
      setCheckingOut(false)
    }
  }

  // Polling payment status
  useEffect(() => {
    let intervalId = null
    
    if (paymentModalOpen && paymentStatus !== 'success' && paymentInfo?.orderId) {
      intervalId = setInterval(async () => {
        try {
          const res = await apiClient.get(`/api/tickets/orders/${paymentInfo.orderId}/status`)
          const data = res.data

          if (data.status === 'COMPLETED') {
            setPaymentStatus('success')
            clearInterval(intervalId)
          } else if (data.status === 'PARTIAL') {
            // Cập nhật QR với số tiền còn thiếu
            setPartialInfo({ paidAmount: data.paidAmount, remainingAmount: data.remainingAmount })
            setPaymentInfo(prev => ({ ...prev, amount: data.remainingAmount }))
            setPaymentStatus('partial')
          }
        } catch (error) {
          console.error("Lỗi khi kiểm tra trạng thái", error)
        }
      }, 3000)
    }
    
    return () => {
      if (intervalId) clearInterval(intervalId)
    }
  }, [paymentModalOpen, paymentStatus, paymentInfo])

  const handleFinishPayment = () => {
    setPaymentModalOpen(false)
    clearCart()
    setSelectedVoucherCode(null)
    closeCart()
    queryClient.invalidateQueries(['myTickets'])
    navigate('/the-cua-toi')
  }

  const validVouchers = vouchersData?.filter(v => v.isActive && (!v.minOrderValue || totalPrice >= v.minOrderValue)) || []

  const bankId = import.meta.env.VITE_VIETQR_BANK_ID || 'MB'
  const accNo = import.meta.env.VITE_VIETQR_ACCOUNT_NO || '03597040604'
  const accName = import.meta.env.VITE_VIETQR_ACCOUNT_NAME || 'SMARTPOOL'
  const vietQRUrl = paymentInfo?.transactionRef 
    ? `https://img.vietqr.io/image/${bankId}-${accNo}-compact2.png?amount=${paymentInfo.amount}&addInfo=${paymentInfo.transactionRef}&accountName=${encodeURIComponent(accName)}` 
    : ''

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

        {items.length > 0 && (
          <div className="cart-summary">
            <Divider style={{ margin: '16px 0' }} />
            
            <div style={{ marginBottom: '16px' }}>
              <Text strong style={{ display: 'block', marginBottom: '8px' }}>Mã giảm giá:</Text>
              <Select
                allowClear
                placeholder="Chọn mã giảm giá"
                style={{ width: '100%' }}
                value={selectedVoucherCode}
                onChange={setSelectedVoucherCode}
                options={validVouchers.map(v => ({
                  value: v.code,
                  label: `${v.code} - Giảm ${v.discountType === 'PERCENTAGE' ? v.discountValue + '%' : new Intl.NumberFormat('vi-VN').format(v.discountValue) + 'đ'}`
                }))}
              />
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
              onClick={handleCheckout} loading={checkingOut}
              className="checkout-btn"
            >
              Thanh Toán Ngay
            </Button>
            <div style={{ textAlign: 'center', marginTop: '12px' }}>
              <Text type="secondary" style={{ fontSize: '12px' }}>
                <span style={{ color: '#52c41a' }}>✓</span> Giao dịch an toàn • Hỗ trợ 24/7
              </Text>
            </div>
          </div>
        )}
      </Drawer>

      <Modal
        title="Thanh toán chuyển khoản"
        open={paymentModalOpen}
        onCancel={() => setPaymentModalOpen(false)}
        footer={null}
        centered
        width={360}
        destroyOnClose
      >
        {(paymentStatus === 'pending' || paymentStatus === 'partial') && paymentInfo && (
          <div style={{ textAlign: 'center', padding: '20px 0' }}>
            <Title level={5} style={{ marginBottom: 16 }}>
              Quét mã QR để thanh toán
            </Title>

            {/* Cảnh báo chuyển khoản thiếu */}
            {paymentStatus === 'partial' && partialInfo && (
              <div style={{
                background: '#fff7e6',
                border: '1px solid #ffa940',
                borderRadius: '8px',
                padding: '10px 14px',
                marginBottom: '14px',
                textAlign: 'left'
              }}>
                <div style={{ fontWeight: 700, color: '#d46b08', marginBottom: 4 }}>⚠️ Chuyển khoản thiếu</div>
                <div style={{ fontSize: 13, color: '#555' }}>
                  Đã nhận: <strong>{new Intl.NumberFormat('vi-VN').format(partialInfo.paidAmount)} đ</strong>
                </div>
                <div style={{ fontSize: 13, color: '#555' }}>
                  Vui lòng quét mã bên dưới để thanh toán nốt: <strong style={{ color: '#f97316' }}>{new Intl.NumberFormat('vi-VN').format(partialInfo.remainingAmount)} đ</strong>
                </div>
              </div>
            )}
            
            {/* Sử dụng VietQR với tài khoản giả lập MB Bank. Nếu có bank thực tế thay vào */}
            <div style={{ background: '#f5f5f5', padding: '16px', borderRadius: '12px', display: 'inline-block', marginBottom: '16px' }}>
              <img 
                src={vietQRUrl} 
                alt="QR Payment" 
                style={{ width: '220px', borderRadius: '8px' }}
              />
            </div>

            <div style={{ marginBottom: 16 }}>
              <Text style={{ fontSize: 16, display: 'block' }}>Số tiền: <strong style={{ color: '#f97316' }}>{new Intl.NumberFormat('vi-VN').format(paymentInfo.amount)} đ</strong></Text>
              <Text style={{ fontSize: 16, display: 'block' }}>Nội dung: <strong>{paymentInfo.transactionRef}</strong></Text>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, color: '#1890ff' }}>
              <Spin size="small" />
              <Text style={{ color: '#1890ff' }}>Đang chờ thanh toán...</Text>
            </div>
          </div>
        )}

        {paymentStatus === 'success' && (
          <Result
            status="success"
            title="Thanh toán thành công!"
            subTitle="Vé đã được phát hành và lưu vào thẻ của bạn."
            extra={[
              <Button type="primary" key="console" onClick={handleFinishPayment}>
                Xem thẻ của tôi
              </Button>
            ]}
          />
        )}
      </Modal>
    </>
  )
}
