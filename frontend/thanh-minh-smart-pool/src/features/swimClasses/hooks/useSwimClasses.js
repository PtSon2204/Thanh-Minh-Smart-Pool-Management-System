import { useQuery } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'

export function useSwimClasses(params, userId) {
  return useQuery({
    queryKey: ['swim-classes', userId, params],
    queryFn: ({ signal }) => swimClassService.getClasses(params, signal),
    enabled: Boolean(userId),
  })
}

export function useSwimClassDetail(classId, userId, scope) {
  return useQuery({
    queryKey: ['swim-class-detail', userId, classId, scope],
    queryFn: ({ signal }) => swimClassService.getClassById(classId, scope, signal),
    enabled: Boolean(classId && userId),
    retry: (failureCount, error) => ![401, 403, 404].includes(error?.response?.status) && failureCount < 2,
  })
}
