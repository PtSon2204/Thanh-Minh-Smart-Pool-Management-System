import { useMutation, useQuery } from '@tanstack/react-query'
import { message } from 'antd'
import ticketService from '../services/ticketService'

export function useSellOfflineTicket() {
  return useMutation({
    mutationFn: (data) => ticketService.sellOffline(data),
    onError: (error) => {
      const msg = error?.response?.data?.message || 'Lỗi khi bán vé.'
      message.error(msg)
    },
  })
}

export function useCreatePendingOrder() {
  return useMutation({
    mutationFn: (data) => ticketService.createPendingOrder(data),
    onError: (error) => {
      const msg = error?.response?.data?.message || 'Lỗi khi tạo đơn hàng.'
      message.error(msg)
    },
  })
}

export function useOrderStatus(orderId, options = {}) {
  return useQuery({
    queryKey: ['orderStatus', orderId],
    queryFn: () => ticketService.checkOrderStatus(orderId),
    enabled: !!orderId,
    ...options
  })
}
