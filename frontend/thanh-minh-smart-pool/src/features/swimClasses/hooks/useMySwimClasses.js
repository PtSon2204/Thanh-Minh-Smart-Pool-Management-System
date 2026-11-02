import { useQuery } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'

export function useMySwimClasses(params, userId) {
  return useQuery({
    queryKey: ['my-swim-classes', userId, params],
    queryFn: ({ signal }) => swimClassService.getMyClasses(params, signal),
    enabled: Boolean(userId),
    refetchOnWindowFocus: true,
  })
}
