import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'
import { useAuthStore } from '../../auth/store/authStore'

export function useSwimClassCoaches(enabled) {
  const session = useAuthStore(state => state.session)
  return useQuery({
    queryKey: ['swim-class-coaches', session?.id],
    queryFn: async () => [{ userId: session.id, username: session.username }],
    staleTime: 60_000,
    enabled: Boolean(enabled && session?.id),
  })
}

export function useCreateSwimClass() {
  return useMutation({ mutationFn: swimClassService.createClass })
}

export function useUpdateSwimClass() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ classId, payload }) => swimClassService.updateClass(classId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['swim-classes'] }),
        queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] }),
      ])
    },
  })
}

export function useUpdateClassSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ classId, sessionId, payload }) => swimClassService.updateSession(classId, sessionId, payload),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['swim-classes'] }),
        queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] }),
      ])
    },
  })
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
