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
