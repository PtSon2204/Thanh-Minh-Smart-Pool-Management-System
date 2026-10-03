import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import voucherService from '../services/voucherService'
import { VOUCHERS_QUERY_KEY } from './useVouchers'

export function getVoucherErrorMessage(error, fallback) {
  const data = error?.response?.data
  const validationError = data?.errors && Object.values(data.errors).flat()[0]
  return validationError || data?.title || data?.message || fallback
}

export function useCreateVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: voucherService.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: VOUCHERS_QUERY_KEY })
      message.success('Đã thêm voucher mới thành công.')
    },
    onError: (error) => {
      message.error(getVoucherErrorMessage(error, 'Không thể tạo voucher.'))
    },
  })
}

export function useUpdateVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }) => voucherService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: VOUCHERS_QUERY_KEY })
      message.success('Đã cập nhật voucher thành công.')
    },
    onError: (error) => {
      message.error(getVoucherErrorMessage(error, 'Không thể cập nhật voucher.'))
    },
  })
}

export function useToggleLockVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id) => voucherService.toggleLock(id),
    onSuccess: (isActive) => {
      queryClient.invalidateQueries({ queryKey: VOUCHERS_QUERY_KEY })
      message.success(isActive ? 'Voucher đã được kích hoạt.' : 'Voucher đã bị khóa.')
    },
    onError: (error) => {
      message.error(getVoucherErrorMessage(error, 'Không thể thay đổi trạng thái voucher.'))
    },
  })
}

export function useDeleteVoucher() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id) => voucherService.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: VOUCHERS_QUERY_KEY })
      message.success('Đã xóa voucher thành công.')
    },
    onError: (error) => {
      message.error(getVoucherErrorMessage(error, 'Không thể xóa voucher.'))
    },
  })
}
