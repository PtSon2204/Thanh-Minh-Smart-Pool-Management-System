import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import ticketTypeService from '../services/ticketTypeService'
import { TICKET_TYPES_QUERY_KEY } from './useTicketTypes'

/**
 * Hook tạo mới loại vé.
 * Sau khi thành công sẽ tự invalidate cache → danh sách tự reload.
 */
export function useCreateTicketType() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ticketTypeService.create,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TICKET_TYPES_QUERY_KEY })
      message.success('Thêm loại vé thành công!')
    },
    onError: (error) => {
      const msg =
        error?.response?.data?.title ||
        error?.response?.data?.message ||
        'Thêm loại vé thất bại. Vui lòng thử lại.'
      message.error(msg)
    },
  })
}
