import { ShoppingCartOutlined } from '@ant-design/icons'
import { Badge } from 'antd'
import { useCartStore } from '../store/cartStore'
import './CartIcon.css'

export default function CartIcon() {
  const items = useCartStore((state) => state.items)
  const toggleCart = useCartStore((state) => state.toggleCart)

  const totalQuantity = items.reduce((sum, item) => sum + item.quantity, 0)

  return (
    <div className="cart-icon-wrapper" onClick={toggleCart}>
      <Badge count={totalQuantity} color="#f97316" offset={[-2, 2]}>
        <div className="cart-icon-circle">
          <ShoppingCartOutlined style={{ fontSize: '20px', color: '#005f8e' }} />
        </div>
      </Badge>
    </div>
  )
}
