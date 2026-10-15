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
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-employees'] }),
      queryClient.invalidateQueries({ queryKey: ['shifts'] }),
    ]),
  })
}

export function useStaffSalaries(employeeId, params, enabled) {
  return useQuery({
    queryKey: ['staff-salaries', employeeId, params],
    queryFn: () => staffService.getSalaries({ ...params, employeeId }),
    enabled: Boolean(enabled && employeeId),
  })
}
