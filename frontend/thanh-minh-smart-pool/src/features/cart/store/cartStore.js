import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'

export const useCartStore = create(
  persist(
    (set) => ({
      items: [],
      isCartOpen: false,

      addToCart: (item) => {
        set((state) => {
          const existing = state.items.find((i) => i.id === item.id)
          if (existing) {
            return {
              items: state.items.map((i) =>
                i.id === item.id ? { ...i, quantity: i.quantity + (item.quantity || 1) } : i
              ),
              isCartOpen: true
            }
          }
          return { items: [...state.items, { ...item, quantity: item.quantity || 1 }], isCartOpen: true }
        })
      },

      removeFromCart: (id) => {
        set((state) => ({ items: state.items.filter((i) => i.id !== id) }))
      },

      updateQuantity: (id, amount) => {
        set((state) => ({
          items: state.items.map((i) => {
            if (i.id === id) {
              const newQuantity = Math.max(1, i.quantity + amount)
              return { ...i, quantity: newQuantity }
            }
            return i
          })
        }))
      },

      clearCart: () => set({ items: [] }),
      
      toggleCart: () => set((state) => ({ isCartOpen: !state.isCartOpen })),
      
      closeCart: () => set({ isCartOpen: false })
    }),
    {
      name: 'thanh-minh-cart',
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({ items: state.items })
    }
  )
)
