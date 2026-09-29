import { useQuery } from '@tanstack/react-query'
import ticketTypeService from '../services/ticketTypeService'

/** Query key dùng chung để invalidate sau khi thêm/sửa/xóa */
export const TICKET_TYPES_QUERY_KEY = ['ticket-types']

/**
 * Hook lấy danh sách loại vé từ API.
 * Tự động cache, refetch khi stale.
 */
export function useTicketTypes() {
  return useQuery({
    queryKey: TICKET_TYPES_QUERY_KEY,
    queryFn: ticketTypeService.getAll,
  })
}
