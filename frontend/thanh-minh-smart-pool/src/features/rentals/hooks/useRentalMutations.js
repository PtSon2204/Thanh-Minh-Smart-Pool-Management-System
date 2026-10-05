import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import { INVENTORY_HISTORY_QUERY_KEY, SERVICES_QUERY_KEY } from '../../services/hooks/useServices'
import rentalService from '../services/rentalService'
import { RENTAL_DETAILS_QUERY_KEY, RENTALS_QUERY_KEY } from './useRentals'

export function getRentalErrorMessage(error, fallback) {
  const data = error?.response?.data
  const validationError = data?.errors && Object.values(data.errors).flat()[0]
  return validationError || data?.title || data?.message || fallback
}

export function useCheckoutRental() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: rentalService.checkout,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: RENTALS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: RENTAL_DETAILS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: INVENTORY_HISTORY_QUERY_KEY })
      message.success('Đã tạo đơn cho thuê.')
    },
  })
}

export function useReturnRental() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: rentalService.returnRental,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: RENTALS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: RENTAL_DETAILS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: INVENTORY_HISTORY_QUERY_KEY })
      message.success('Đã nhận lại sản phẩm cho thuê.')
    },
  })
}

export function useReturnRentalQuantity() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: rentalService.returnQuantity,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: RENTALS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: RENTAL_DETAILS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: INVENTORY_HISTORY_QUERY_KEY })
      message.success('Đã nhận lại số lượng sản phẩm đã chọn.')
    },
  })
}
