import { useQuery } from '@tanstack/react-query'
import rentalService from '../services/rentalService'

export const RENTALS_QUERY_KEY = ['rentals']
export const RENTAL_DETAILS_QUERY_KEY = ['rental-details']

export function useRentals(params) {
  return useQuery({
    queryKey: [...RENTALS_QUERY_KEY, params],
    queryFn: () => rentalService.getAll(params),
  })
}

export function useRentalDetails(orderId, productId, enabled) {
  return useQuery({
    queryKey: [...RENTAL_DETAILS_QUERY_KEY, orderId, productId],
    queryFn: () => rentalService.getDetails(orderId, productId),
    enabled: Boolean(enabled && orderId && productId),
  })
}
