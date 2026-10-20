import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import staffService from '../services/staffService'

export const STAFFS_QUERY_KEY = ['staffs']

export function useStaffs(params, enabled = true) {
  return useQuery({
    queryKey: [...STAFFS_QUERY_KEY, params],
    queryFn: () => staffService.getAll(params),
    enabled,
  })
}

export function useStaffOptions(enabled) {
  return useQuery({
    queryKey: ['staff-options'],
    queryFn: staffService.getOptions,
    enabled,
  })
}

export function useStaffSchedule(employeeId, params, enabled) {
  return useQuery({
    queryKey: ['staff-schedule', employeeId, params],
    queryFn: () => staffService.getSchedule({ ...params, employeeId }),
    enabled: Boolean(enabled && employeeId),
  })
}

export function useStaffSchedulePage({ scope, sessionUserId, params, enabled }) {
  return useQuery({
    queryKey: ['staff-schedule-page', scope, sessionUserId, params.fromDate, params.toDate, params.employeeId, params.status, params.pageIndex, params.pageSize],
    queryFn: ({ signal }) => staffService.getCompleteSchedule(scope, params, signal),
    enabled: Boolean(enabled && sessionUserId),
  })
}

export function useAttendanceSchedule(sessionUserId, date, enabled) {
  return useQuery({
    queryKey: ['staff-attendance', sessionUserId, date],
    queryFn: ({ signal }) => staffService.getCompleteSchedule('all', { fromDate: date, toDate: date }, signal),
    enabled: Boolean(enabled && sessionUserId && date),
  })
}

export function useScheduleEmployees(enabled) {
  return useQuery({
    queryKey: ['staff-schedule-employees'],
    queryFn: ({ signal }) => staffService.getScheduleEmployees(signal),
    enabled,
  })
}

export function useAssignShiftBatch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: staffService.assignShiftBatch,
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['staff-attendance'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-employees'] }),
      queryClient.invalidateQueries({ queryKey: ['shifts'] }),
    ]),
  })
}

export function useCancelSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: staffService.cancelSchedule,
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['staff-attendance'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-employees'] }),
    ]),
  })
}

function useAttendanceMutation(mutationFn) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['staff-attendance'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
    ]),
  })
}

export function useCheckIn() {
  return useAttendanceMutation(({ id, reason }) => staffService.checkIn(id, { reason }))
}

export function useCheckOut() {
  return useAttendanceMutation(({ id, reason }) => staffService.checkOut(id, { reason }))
}

export function useCorrectAttendance() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...data }) => staffService.correctAttendance(id, data),
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['staff-attendance'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-attendance-history'] }),
    ]),
  })
}

export function useAttendanceHistory(assignmentId, pageIndex = 1, enabled = true) {
  return useQuery({
    queryKey: ['staff-attendance-history', assignmentId, pageIndex],
    queryFn: ({ signal }) => staffService.getAttendanceHistory(assignmentId, { pageIndex, pageSize: 20 }, signal),
    enabled: Boolean(enabled && assignmentId),
    retry: false,
  })
}

export function useStaffSalaries(employeeId, params, enabled) {
  return useQuery({
    queryKey: ['staff-salaries', employeeId, params],
    queryFn: () => staffService.getSalaries({ ...params, employeeId }),
    enabled: Boolean(enabled && employeeId),
  })
}
