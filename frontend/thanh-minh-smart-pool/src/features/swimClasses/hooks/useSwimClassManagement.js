import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
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
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ classId, sessions }) => swimClassService.createSessions(classId, sessions),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['swim-classes'] })
      void queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] })
    },
  })
}

export function useActivateSwimClass() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: swimClassService.activate,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['swim-classes'] })
      void queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] })
    },
  })
}
