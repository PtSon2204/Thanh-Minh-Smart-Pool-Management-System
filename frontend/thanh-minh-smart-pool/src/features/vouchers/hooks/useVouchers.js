import { useQuery } from '@tanstack/react-query'
import voucherService from '../services/voucherService'

export const VOUCHERS_QUERY_KEY = ['vouchers']

export function useVouchers(params) {
  return useQuery({
    queryKey: [...VOUCHERS_QUERY_KEY, params],
    queryFn: () => voucherService.getAll(params),
  })
}
