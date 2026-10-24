import { useMutation, useQuery } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'

export function useSwimClassCoaches(enabled) {
  return useQuery({
    queryKey: ['swim-class-coaches'],
    queryFn: ({ signal }) => swimClassService.getCoaches(signal),
    staleTime: 60_000,
    enabled,
  })
}

export function useCreateSwimClass() {
  return useMutation({ mutationFn: swimClassService.createClass })
}

export function useCreateClassSessions() {
  return useMutation({
    mutationFn: ({ classId, sessions }) => swimClassService.createSessions(classId, sessions),
  })
}

export function useActivateSwimClass() {
  return useMutation({ mutationFn: swimClassService.activate })
}
