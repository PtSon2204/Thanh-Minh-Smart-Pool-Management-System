import { useQuery } from '@tanstack/react-query'
import paymentService from '../services/paymentService'

export const PAYMENTS_QUERY_KEY = ['payments']

export function usePayments(params) {
  return useQuery({
    queryKey: [...PAYMENTS_QUERY_KEY, params],
    queryFn: () => paymentService.getAll(params),
    placeholderData: (previousData) => previousData,
  })
}

export function usePaymentDetail(id) {
  return useQuery({
    queryKey: ['payment-detail', id],
    queryFn: () => paymentService.getById(id),
    enabled: !!id,
  })
}
