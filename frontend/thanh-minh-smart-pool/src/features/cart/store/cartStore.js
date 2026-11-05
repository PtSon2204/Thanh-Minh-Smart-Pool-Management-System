import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'

export const useCartStore = create(
  persist(
    (set) => ({
      items: [],
      isCartOpen: false,

      addToCart: (item) => {
        set((state) => {
          const itemEntries = item.ticketCategory === 'VE_LUOT'
            ? (item.requestedEntries ? Math.max(1, parseInt(item.requestedEntries, 10) || 1) : 1)
            : undefined

          const existing = state.items.find((i) => i.id === item.id)
          if (existing) {
            return {
              items: state.items.map((i) =>
                i.id === item.id ? { 
                  ...i, 
                  quantity: i.quantity + (item.quantity || 1),
                  ...(itemEntries !== undefined ? { requestedEntries: itemEntries } : {})
                } : i
              ),
              isCartOpen: true
            }
          }

          const newItem = {
            ...item,
            quantity: item.quantity || 1,
            ...(itemEntries !== undefined ? { requestedEntries: itemEntries } : {})
          }
          return { items: [...state.items, newItem], isCartOpen: true }
        })
      },

      updateRequestedEntries: (id, entries) => {
        set((state) => ({
          items: state.items.map((i) => {
            if (i.id === id) {
              const validEntries = entries === '' || entries === null || entries === undefined
                ? 1
                : Math.max(1, parseInt(entries, 10) || 1)
              return { ...i, requestedEntries: validEntries }
            }
            return i
          })
        }))
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

      removePurchasedItems: (purchasedItems) => set((state) => ({
        items: state.items.flatMap((item) => {
          const purchased = purchasedItems.filter((entry) => entry.ticketTypeId === item.id)
            .reduce((sum, entry) => sum + entry.quantity, 0)
          const quantity = item.quantity - purchased
          return quantity > 0 ? [{ ...item, quantity }] : []
        })
      })),
      
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
