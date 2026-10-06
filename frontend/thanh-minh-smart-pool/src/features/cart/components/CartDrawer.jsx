import { Drawer, Button, Typography, Divider } from 'antd'
import { DeleteOutlined, MinusOutlined, PlusOutlined, SendOutlined } from '@ant-design/icons'
import { useCartStore } from '../store/cartStore'
import { useNavigate } from 'react-router-dom'
import './CartDrawer.css'

const { Text, Title } = Typography

export default function CartDrawer() {
  const { isCartOpen, closeCart, items, removeFromCart, updateQuantity } = useCartStore()
  const navigate = useNavigate()

  const totalPrice = items.reduce((sum, item) => sum + item.price * item.quantity, 0)
  const totalQuantity = items.reduce((sum, item) => sum + item.quantity, 0)

  const handleCheckout = () => {
    closeCart()
    navigate('/thanh-toan') // assuming a checkout page or just alert for now
  }

  return (
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
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <Text style={{ fontSize: '16px', color: '#666' }}>Tổng tiền:</Text>
            <Title level={4} style={{ margin: 0, color: '#f97316' }}>
              {new Intl.NumberFormat('vi-VN').format(totalPrice)} đ
            </Title>
          </div>
          <Button 
            type="primary" 
            size="large" 
            block 
            icon={<SendOutlined />}
            onClick={handleCheckout}
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
  )
}
