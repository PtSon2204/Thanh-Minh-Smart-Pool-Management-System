import { useMutation, useQueryClient } from '@tanstack/react-query'
import { message } from 'antd'
import ticketTypeService from '../services/ticketTypeService'
import { TICKET_TYPES_QUERY_KEY } from './useTicketTypes'

export function useUpdateTicketType() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }) => ticketTypeService.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TICKET_TYPES_QUERY_KEY })
      message.success('Cập nhật loại vé thành công!')
    },
    onError: (error) => {
      const msg = error?.response?.data?.title || error?.response?.data?.message || 'Cập nhật thất bại.'
      message.error(msg)
    },
  })
}

export function useToggleLockTicketType() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id) => ticketTypeService.toggleLock(id),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: TICKET_TYPES_QUERY_KEY })
      message.success(data.message || 'Thay đổi trạng thái thành công!')
    },
    onError: () => {
      message.error('Không thể thay đổi trạng thái loại vé.')
    },
  })
}
