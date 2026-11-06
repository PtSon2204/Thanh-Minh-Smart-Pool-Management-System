import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'

export const classRegistrationKey = (userId, classId) => ['my-class-registration', userId, classId]

export function useMyClassRegistration(classId, userId) {
  return useQuery({
    queryKey: classRegistrationKey(userId, classId),
    enabled: Boolean(classId && userId),
    queryFn: async ({ signal }) => {
      try {
        return await swimClassService.getMyRegistration(classId, signal)
      } catch (error) {
        if (error?.response?.status === 404) return null
        throw error
      }
    },
    refetchOnWindowFocus: true,
    retry: (attempt, error) => ![401, 403].includes(error?.response?.status) && attempt < 2,
  })
}

export function useRegisterForClass(userId, classId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => swimClassService.register(classId),
    retry: false,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: classRegistrationKey(userId, classId) }),
        queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] }),
        queryClient.invalidateQueries({ queryKey: ['swim-classes'] }),
        queryClient.invalidateQueries({ queryKey: ['my-swim-classes', userId] }),
      ])
    },
  })
}

async function refreshRegistrationViews(queryClient) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['my-class-registration'] }),
    queryClient.invalidateQueries({ queryKey: ['my-swim-classes'] }),
    queryClient.invalidateQueries({ queryKey: ['class-registrations'] }),
    queryClient.invalidateQueries({ queryKey: ['swim-class-detail'] }),
    queryClient.invalidateQueries({ queryKey: ['swim-classes'] }),
  ])
}

export function useCancelMyClassRegistration() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (classId) => swimClassService.cancelMyRegistration(classId),
    retry: false,
    onSuccess: () => refreshRegistrationViews(queryClient),
  })
}

export function useClassRegistrations(classId, params, enabled) {
  return useQuery({
    queryKey: ['class-registrations', classId, params],
    queryFn: ({ signal }) => swimClassService.getClassRegistrations(classId, params, signal),
    enabled: Boolean(classId && enabled),
    retry: (attempt, error) => ![401, 403, 404].includes(error?.response?.status) && attempt < 2,
  })
}

export function useCancelClassRegistration() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ classId, registrationId, payload }) =>
      swimClassService.cancelClassRegistration(classId, registrationId, payload),
    retry: false,
    onSuccess: () => refreshRegistrationViews(queryClient),
  })
}
