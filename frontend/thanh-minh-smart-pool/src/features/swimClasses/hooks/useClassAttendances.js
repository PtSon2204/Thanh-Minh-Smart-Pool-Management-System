import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { swimClassService } from '../services/swimClassService'

export function useMyClassAttendances(classId, userId, pageIndex) {
  return useQuery({
    queryKey: ['my-class-attendances', userId, classId, pageIndex],
    queryFn: ({ signal }) => swimClassService.getMyClassAttendances(classId,
      { pageIndex, pageSize: 20 }, signal),
    enabled: Boolean(classId && userId),
    refetchOnWindowFocus: true,
    retry: (attempt, error) => ![401, 403, 404].includes(error?.response?.status) && attempt < 2,
  })
}

export function useSessionAttendances(classId, sessionId, coachId, params) {
  return useQuery({
    queryKey: ['class-session-attendances', coachId, classId, sessionId, params],
    queryFn: ({ signal }) => swimClassService.getSessionAttendances(classId, sessionId, params, signal),
    enabled: Boolean(classId && sessionId && coachId),
    refetchOnWindowFocus: true,
    retry: (attempt, error) => ![401, 403, 404].includes(error?.response?.status) && attempt < 2,
  })
}

export function useRecordClassAttendance(classId, sessionId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ registrationId, status, image, expectedUpdatedAt }) =>
      swimClassService.recordClassAttendance(classId, sessionId, registrationId, status, image, expectedUpdatedAt),
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['class-session-attendances'] }),
      queryClient.invalidateQueries({ queryKey: ['my-class-attendances'] }),
    ]),
  })
}

export function useUpdateClassProgressNote(classId, sessionId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ registrationId, progressNote, expectedUpdatedAt }) =>
      swimClassService.updateClassProgressNote(classId, sessionId, registrationId, progressNote, expectedUpdatedAt),
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['class-session-attendances'] }),
      queryClient.invalidateQueries({ queryKey: ['my-class-attendances'] }),
    ]),
  })
}
