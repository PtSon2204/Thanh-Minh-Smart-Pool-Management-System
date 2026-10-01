import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import ticketTypeService from '../services/ticketTypeService'

/** Query key dùng chung để invalidate sau khi thêm/sửa/xóa */
export const TICKET_TYPES_QUERY_KEY = ['ticket-types']

/**
 * Hook lấy danh sách loại vé từ API.
 * Tự động cache, refetch khi stale.
 */
export function useTicketTypes(params) {
  return useQuery({
    queryKey: [...TICKET_TYPES_QUERY_KEY, params],
    queryFn: () => ticketTypeService.getAll(params),
  })
}

/** Hook xóa loại vé */
export function useDeleteTicketType() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id) => ticketTypeService.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TICKET_TYPES_QUERY_KEY })
    },
  })
}
