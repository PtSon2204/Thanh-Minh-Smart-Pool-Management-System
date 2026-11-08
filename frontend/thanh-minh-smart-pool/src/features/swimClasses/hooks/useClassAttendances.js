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

export function useSubmitClassAttendance(userId, classId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ sessionId, image }) => swimClassService.submitClassAttendance(classId, sessionId, image),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['my-class-attendances', userId, classId] }),
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

export function useReviewClassAttendance(classId, sessionId) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ attendanceId, payload }) => swimClassService.reviewClassAttendance(
      classId, sessionId, attendanceId, payload),
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['class-session-attendances'] }),
      queryClient.invalidateQueries({ queryKey: ['my-class-attendances'] }),
    ]),
  })
}
