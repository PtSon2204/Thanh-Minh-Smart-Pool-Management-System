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
      message.success('Đã thêm voucher.')
    },
  })
}
