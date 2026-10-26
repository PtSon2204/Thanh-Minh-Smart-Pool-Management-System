import test, { beforeEach } from 'node:test'
import assert from 'node:assert/strict'

const storage = new Map()
globalThis.sessionStorage = {
  getItem: (key) => storage.get(key) ?? null,
  setItem: (key, value) => storage.set(key, String(value)),
  removeItem: (key) => storage.delete(key),
  clear: () => storage.clear(),
}

const { useCartStore } = await import('./cartStore.js')

const ticketA = { id: 'ticket-a', name: 'Vé lượt', price: 50000 }
const ticketB = { id: 'ticket-b', name: 'Vé tháng', price: 300000 }

beforeEach(() => {
  storage.clear()
  useCartStore.setState({ items: [], isCartOpen: false })
})

test('UTCID01 (N) - Thêm vé mới -> Mặc định số lượng 1 và mở giỏ', () => {
  useCartStore.getState().addToCart(ticketA)

  assert.deepEqual(useCartStore.getState().items, [{ ...ticketA, quantity: 1 }])
  assert.equal(useCartStore.getState().isCartOpen, true)
})

test('UTCID02 (N) - Thêm vé đã có -> Cộng số lượng vào dòng hiện tại', () => {
  const { addToCart } = useCartStore.getState()
  addToCart({ ...ticketA, quantity: 2 })
  addToCart({ ...ticketA, quantity: 3 })

  assert.deepEqual(useCartStore.getState().items, [{ ...ticketA, quantity: 5 }])
})

test('UTCID03 (N) - Xóa vé theo id -> Chỉ loại dòng có id tương ứng', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 1 }, { ...ticketB, quantity: 2 }] })

  useCartStore.getState().removeFromCart(ticketA.id)

  assert.deepEqual(useCartStore.getState().items, [{ ...ticketB, quantity: 2 }])
})

test('UTCID04 (B) - Giảm số lượng ở mức 1 -> Số lượng vẫn là 1', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 1 }] })

  useCartStore.getState().updateQuantity(ticketA.id, -1)

  assert.equal(useCartStore.getState().items[0].quantity, 1)
})

test('UTCID05 (N) - Tăng số lượng -> Cộng amount vào vé cùng id', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 1 }] })

  useCartStore.getState().updateQuantity(ticketA.id, 2)

  assert.equal(useCartStore.getState().items[0].quantity, 3)
})

test('UTCID06 (N) - Xóa giỏ -> Danh sách vé rỗng', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 1 }] })

  useCartStore.getState().clearCart()

  assert.deepEqual(useCartStore.getState().items, [])
})

test('UTCID07 (N) - Mua một phần số lượng -> Giữ lại số vé chưa mua', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 4 }] })

  useCartStore.getState().removePurchasedItems([{ ticketTypeId: ticketA.id, quantity: 2 }])

  assert.deepEqual(useCartStore.getState().items, [{ ...ticketA, quantity: 2 }])
})

test('UTCID08 (N) - Mua hết số lượng trong giỏ -> Loại vé khỏi giỏ', () => {
  useCartStore.setState({ items: [{ ...ticketA, quantity: 2 }, { ...ticketB, quantity: 1 }] })

  useCartStore.getState().removePurchasedItems([{ ticketTypeId: ticketA.id, quantity: 2 }])

  assert.deepEqual(useCartStore.getState().items, [{ ...ticketB, quantity: 1 }])
})

test('UTCID09 (N) - toggleCart và closeCart -> Đổi trạng thái mở rồi đóng giỏ', () => {
  const { toggleCart, closeCart } = useCartStore.getState()

  toggleCart()
  assert.equal(useCartStore.getState().isCartOpen, true)
  closeCart()
  assert.equal(useCartStore.getState().isCartOpen, false)
})
