import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import serviceService from '../services/serviceService'
import { INVENTORY_HISTORY_QUERY_KEY, SERVICES_QUERY_KEY } from './useServices'

export function getServiceErrorMessage(error, fallback) {
  const data = error?.response?.data
  const validationError = data?.errors && Object.values(data.errors).flat()[0]
  return validationError || data?.title || data?.message || fallback
}

export function useCreateService() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: serviceService.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      message.success('Đã thêm dịch vụ.')
    },
  })
}

export function useUpdateService() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }) => serviceService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      message.success('Đã cập nhật dịch vụ.')
    },
  })
}

export function useAdjustStock() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }) => serviceService.adjustStock(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SERVICES_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: INVENTORY_HISTORY_QUERY_KEY })
      message.success('Đã điều chỉnh tồn kho.')
    },
  })
}
